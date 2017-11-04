﻿using Octgn.Communication.TransportSDK;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class TcpConnection : ConnectionBase
    {
        private static ILogger Log = LoggerFactory.Create(nameof(TcpConnection));

        private readonly TcpClient _client;
        private PacketBuilder _packetBuilder;

        /// <summary>
        /// Was this connection created from a IConnectionListener?
        /// </summary>
        internal bool IsListenerConnection => RemoteHost == null;

        internal string RemoteHost { get; }

        private readonly string _toString;

        public override bool IsConnected => _isConnected;
        private bool _isConnected;

        public TcpConnection(TcpClient client)
        {
            _client = client;
            _toString = $"ListenerConnection:{_client.Client.LocalEndPoint}:{ConnectionId}";
            _packetBuilder = new PacketBuilder();
            _isConnected = true;
        }

        public TcpConnection(string remoteHost)
        {
            _client = new TcpClient();
            RemoteHost = remoteHost;
            _toString = $"Connection:{RemoteHost}:{ConnectionId}";
            _packetBuilder = new PacketBuilder();
            _isConnected = false;
        }

        protected TcpConnection(TcpConnection connection)
        {
            if (connection.RemoteHost == null) throw new ArgumentException("connection can't be created from this connection.", nameof(connection));

            _client = new TcpClient();
            RemoteHost = connection.RemoteHost;
            _toString = connection.ToString();
            _isConnected = false;
            try { } finally {
                _packetBuilder = connection._packetBuilder;
                connection._packetBuilder = null;
            }
        }

        private bool _calledConnect;
        private readonly object L_CALLEDCONNECT = new object();
        public override async Task Connect()
        {
            if (IsListenerConnection) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)}");
            if (IsClosed) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)} on a closed connection");

            lock (L_CALLEDCONNECT) {
                if (_calledConnect) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)} more than once");
                _calledConnect = true;
            }

            Log.Info($"{this}: Starting to {nameof(Connect)} to {RemoteHost}...");

            var hostParts = RemoteHost.Split(':');
            if (hostParts.Length != 2)
                throw new FormatException($"{this}: {nameof(RemoteHost)} is in the wrong format '{RemoteHost}.' Should be in the format 'hostname:port' for example 'localhost:4356' or 'jumbo.fried.jims.aquarium:4453'");
            var host = hostParts[0];
            var port = int.Parse(hostParts[1]);

            Log.Info($"{this}: Resolving IPAddresses for {host}...");
            IPAddress[] addresses = null;
            await ResilliantTask.Run(async () => {
                addresses = await Dns.GetHostAddressesAsync(host);
            });

            addresses = addresses ?? new IPAddress[0];

            Log.Info($"{this}: Found {addresses.Length.ToString()}: {string.Join(", ", (object[])addresses)}");

            if (addresses.Length == 0) throw new InvalidOperationException($"Unable to find any IP address for host {host} :(");

            bool connected = false;
            foreach (var address in addresses) {
                try {
                    Log.Info($"{this}: Trying to connect to {address}:{port}...");
                    await _client.ConnectAsync(address, port);
                    Log.Info($"{this}: Connected to {address}:{port}...");
                    connected = true;
                    break;
                } catch (Exception ex) {
                    Log.Warn($"{this}: Error connecting to {RemoteHost} using the address {address}", ex);
                }
            }

            if (connected) {
                await base.Connect();
                _isConnected = true;
            } else {
                throw new Exception($"{this}: Unable to connect to host {host}");
            }
        }

        private readonly ManualResetEventSlim E_READINGCOMPLETE = new ManualResetEventSlim(true);

        private bool _calledReadPackets;
        private readonly object L_CALLEDREADPACKETS = new object();
        protected override async Task ReadPacketsAsync()
        {
            try {
                Log.Info(this.ToString() + ": " + nameof(ReadPacketsAsync));
                // NO REENTRY. These connections are single shot.
                lock (L_CALLEDREADPACKETS) {
                    if (_calledReadPackets) throw new InvalidOperationException($"{this}: Already called {nameof(ReadPacketsAsync)}");
                    _calledReadPackets = true;
                    E_READINGCOMPLETE.Reset();
                }
                var client = _client;
                var buffer = new byte[256];
                while (!base.IsClosed) {
                    var count = 0;
                    try {
                        var stream = client?.GetStream();
                        if (stream == null) return;

                        count = await stream.ReadAsync(buffer, 0, 256)
                            .ConfigureAwait(false);
                    } catch (IOException ex) {
                        throw new DisconnectedException(this.ToString(), ex);
                    } catch (ObjectDisposedException) {
                        throw new DisconnectedException(this.ToString());
                    }

                    if (count == 0) return;

                    foreach (var packet in _packetBuilder.AddData(Serializer, buffer, count)) {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                        // This is OK because the Task has a catch all
                        Task.Run(async () => {
                            try {
                                await FirePacketReceived(packet);
                            } catch (Exception ex) {
                                Log.Error($"{this} FirePacketReceived", ex);
                            }
                        });
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    }
                }
            } catch (DisconnectedException) {
                Log.Info($"{this}: Disconnected");
            } catch (Exception ex) {
                Log.Error($"{this}", ex);
                Signal.Exception(ex, this.ToString());
            } finally {
                E_READINGCOMPLETE.Set();
                IsClosed = true;
            }
        }

        protected override async Task SendPacket(Packet packet)
        {
            try {
                packet.Sent = DateTimeOffset.Now;
                var packetData = Packet.Serialize(packet, Serializer);
                if (packetData == null)
                    throw new InvalidOperationException($"{this}: packetdata is null");

                var stream = _client?.GetStream();
                if (stream == null) throw new InvalidOperationException($"{this}: Can't send packet {packet.Id}, there's no open connection");


                await stream.WriteAsync(packetData, 0, packetData.Length, _connectionCloseCancellation.Token);
                await base.SendPacket(packet);
            } catch (ObjectDisposedException) {
                IsClosed = true;
                throw new DisconnectedException(this.ToString());
            } catch (Exception ex) {
                IsClosed = true;
                throw new DisconnectedException(this.ToString(), ex);
            }
        }

        private readonly CancellationTokenSource _connectionCloseCancellation = new CancellationTokenSource();

        protected override void Close(ConnectionClosedEventArgs args)
        {
            Log.Info($"{this}: Close");
            if (_isConnected) {
                _isConnected = false;
                try {
                    _client?.Client.Disconnect(false);
                } catch { }
                _connectionCloseCancellation.Cancel();
                _client?.Close();
                try {
                    if (!E_READINGCOMPLETE.Wait(30000)) throw new TimeoutException($"{this}: Timed out waiting for the read loop to complete.");
                } catch (ObjectDisposedException) { }
                base.Close(args);
            }
        }

        public override IConnection Clone() => new TcpConnection(this);

        public override string ToString() => _toString;

        public override void Dispose()
        {
            Log.Info($"{this}: Dispose");
            if (!this.IsClosed) this.IsClosed = true;
            try {
                E_READINGCOMPLETE.Dispose();
                _connectionCloseCancellation?.Dispose();
            } finally {
                base.Dispose();
            }
        }
    }
}