using Octgn.Communication.TransportSDK;
using Octgn.Communication.Utility;
using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class TcpConnection : ConnectionBase
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(TcpConnection));
#pragma warning restore IDE1006 // Naming Styles

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
        public override async Task Connect(CancellationToken cancellationToken = default(CancellationToken))
        {
            if (IsListenerConnection) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)}");
            if (IsClosed) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)} on a closed connection");

            lock (L_CALLEDCONNECT) {
                if (_calledConnect) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)} more than once");
                _calledConnect = true;
            }

            Log.Info($"{this}: Starting to {nameof(Connect)} to {RemoteHost}...");

            var methodRuntime = new Stopwatch();
            methodRuntime.Start();

            var hostParts = RemoteHost.Split(':');
            if (hostParts.Length != 2)
                throw new FormatException($"{this}: {nameof(RemoteHost)} is in the wrong format '{RemoteHost}.' Should be in the format 'hostname:port' for example 'localhost:4356' or 'jumbo.fried.jims.aquarium:4453'");
            var host = hostParts[0];
            var port = int.Parse(hostParts[1]);

            Log.Info($"{this}: Resolving IPAddresses for {host}...");
            IPAddress[] addresses = null;
            cancellationToken.ThrowIfCancellationRequested();
            await ResilliantTask.Run(async () => addresses = await Dns.GetHostAddressesAsync(host), cancellationToken: cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();

            addresses = addresses ?? new IPAddress[0];

            Log.Info($"{this}: Found {addresses.Length.ToString()}: {string.Join(", ", (object[])addresses)}");

            if (addresses.Length == 0) throw new InvalidOperationException($"Unable to find any IP address for host {host} :(");

            var connected = false;
            foreach (var address in addresses) {
                try {
                    Log.Info($"{this}: Trying to connect to {address}:{port}...");
                    cancellationToken.ThrowIfCancellationRequested();
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

        private Task _processPacketsTask = Task.CompletedTask;

        protected override async Task ReadPacketsAsync() {
            Log.Info(this + ": " + nameof(ReadPacketsAsync));

            var client = _client;
            var buffer = new byte[256];
            while (_isConnected) {
                var count = 0;
                try {
                    var stream = client?.GetStream();
                    if (stream == null) return;

                    count = await stream.ReadAsync(buffer, 0, 256);
                } catch (IOException ex) {
                    throw new DisconnectedException(this.ToString(), ex);
                } catch (ObjectDisposedException) {
                    throw new DisconnectedException(this.ToString());
                }

                if (count == 0) return;

                foreach (var packet in _packetBuilder.AddData(Serializer, buffer, count)) {
                    // Don't await this, it causes deadlocks.
                    _processPacketsTask = _processPacketsTask.ContinueWith(async (prevTask) => {
                        // Running this as ContinuesWith will fire these synchronusly
                        // We assign back to _processPacketsTask so that we can track all of these tasks
                        // This effectively combines all of these tasks into one
                        try {
                            if (!_isConnected) {
                                Log.Warn($"{this}: Connection is closed. Dropping packet {packet}");
                                return;
                            }
                            await ProcessReceivedPacket(packet, ClosedCancellationToken);
                        } catch (Exception ex) {
                            Signal.Exception(ex);
                            IsClosed = true;
                        }
                    }).Unwrap();
                }
            }
        }

        public override void Dispose() {
            _processPacketsTask.Wait();
            base.Dispose();
        }

        protected override async Task SendPacketImplementation(Packet packet, CancellationToken cancellationToken)
        {
            try {
                packet.Sent = DateTimeOffset.Now;
                var packetData = Packet.Serialize(packet, Serializer);
                if (packetData == null)
                    throw new InvalidOperationException($"{this}: packetdata is null");

                var stream = _client?.GetStream();
                if (stream == null) throw new InvalidOperationException($"{this}: Can't send packet {packet.Id}, there's no open connection");

                using (var combinedCancellations = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ClosedCancellationToken)) {
                    await stream.WriteAsync(packetData, 0, packetData.Length, combinedCancellations.Token);
                }
            } catch (ObjectDisposedException) {
                IsClosed = true;
                throw new DisconnectedException(this.ToString());
            } catch (Exception ex) {
                IsClosed = true;
                throw new DisconnectedException(this.ToString(), ex);
            }
        }

        protected override void Close(ConnectionClosedEventArgs args)
        {
            Log.Info($"{this}: Close");
            if (_isConnected) {
                _isConnected = false;
                try {
                    _client?.Close();
                } catch (Exception ex) {
                    Log.Warn($"{this}: {nameof(Close)}", ex);
                }
                base.Close(args);
            }
        }

        public override IConnection Clone() => new TcpConnection(this);

        public override string ToString() => _toString;
    }
}
