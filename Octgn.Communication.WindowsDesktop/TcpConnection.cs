using Octgn.Communication.TransportSDK;
using System;
using System.IO;
using System.Linq;
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
        /// <summary>
        /// You must close the NetworkStream when you are through sending and receiving data. Closing TcpClient does not release the NetworkStream. -- https://docs.microsoft.com/en-us/dotnet/api/system.net.sockets.tcpclient.getstream?f1url=https%3A%2F%2Fmsdn.microsoft.com%2Fquery%2Fdev15.query%3FappId%3DDev15IDEF1%26l%3DEN-US%26k%3Dk(System.Net.Sockets.TcpClient.GetStream);k(TargetFrameworkMoniker-.NETFramework,Version%3Dv4.7);k(DevLang-csharp)%26rd%3Dtrue&view=netframework-4.7.1
        /// </summary>
        private NetworkStream _clientStream;
        private readonly PacketBuilder _packetBuilder;
        private readonly ISerializer _serializer;

        internal bool IsListenerConnection { get; }

        /// <summary>
        /// Creates a <see cref="TcpConnection"/> from and already open <see cref="TcpClient"/>
        /// </summary>
        /// <param name="client"></param>
        /// <param name="serializer"></param>
        /// <param name="handshaker"></param>
        public TcpConnection(TcpClient client, ISerializer serializer, IHandshaker handshaker)
            : base(client.Client.RemoteEndPoint.ToString(), handshaker) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _clientStream = _client.GetStream() ?? throw new ArgumentNullException(nameof(_client) + "." + nameof(_client.GetStream));

            _packetBuilder = new PacketBuilder();
            IsListenerConnection = true;

            TransitionState(ConnectionState.Connecting);
            TransitionState(ConnectionState.Handshaking);
        }

        public TcpConnection(string remoteHost, ISerializer serializer, IHandshaker handshaker)
            : base(remoteHost, handshaker) {
            _client = new TcpClient();
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _packetBuilder = new PacketBuilder();
        }

        protected TcpConnection(TcpConnection connection)
            : base (connection.RemoteAddress, connection.Handshaker) {
            _client = new TcpClient();
            _serializer = connection._serializer ?? throw new ArgumentException("Serializer is null", nameof(connection));
            _packetBuilder = new PacketBuilder(connection._packetBuilder);
        }

        protected override void OnConnectionStateChanged(object sender, ConnectionStateChangedEventArgs args) {
            switch (args.NewState) {
                case ConnectionState.Handshaking:
                    ReadPackets().SignalOnException();
                    break;
                case ConnectionState.Closed:
                    try {
                        Log.Info($"{this}: Closing tcp client");
                        _clientStream?.Close();
                        _client.Close();
                    } catch (Exception ex) {
                        Log.Warn($"{this}: {nameof(OnConnectionStateChanged)}", ex);
                    }
                    break;
            }

            base.OnConnectionStateChanged(sender, args);
        }

        protected override async Task ConnectImpl(CancellationToken cancellationToken = default(CancellationToken)) {
            if (IsListenerConnection) throw new InvalidOperationException($"{this}: Cannot call {nameof(Connect)}");

            Log.Info($"{this}: Starting to {nameof(Connect)} to {RemoteAddress}...");

            var hostParts = RemoteAddress.Split(':');
            if (hostParts.Length != 2)
                throw new FormatException($"{this}: {nameof(RemoteAddress)} is in the wrong format '{RemoteAddress}.' Should be in the format 'hostname:port' for example 'localhost:4356' or 'jumbo.fried.jims.aquarium:4453'");
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

            foreach (var address in addresses) {
                try {
                    Log.Info($"{this}: Trying to connect to {address}:{port}...");

                    cancellationToken.ThrowIfCancellationRequested();

                    await _client.ConnectAsync(address, port);

                    _clientStream = _client.GetStream() ?? throw new InvalidOperationException("Null stream " + nameof(_client) + "." + nameof(_client.GetStream));

                    Log.Info($"{this}: Connected to {address}:{port}...");

                    return;
                } catch (Exception ex) {
                    Log.Warn($"{this}: Error connecting to {RemoteAddress} using the address {address}", ex);
                }
            }

            throw new Exception($"{this}: Unable to connect to host {host}. Check logs for previous errors.");
        }

        protected override async Task SendImpl(Packet packet, CancellationToken cancellationToken) {
            try {
                if (packet == null) throw new ArgumentNullException(nameof(packet));

                var packetData = Packet.Serialize(packet, _serializer)
                    ?? throw new InvalidOperationException($"{this}: Packet serialization returned null");

                using (var combinedCancellations = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, ClosedCancellationToken)) {
                    await _clientStream.WriteAsync(packetData, 0, packetData.Length, combinedCancellations.Token);
                }
            } catch (DisconnectedException) {
                throw;
            } catch (ObjectDisposedException) {
                throw new DisconnectedException(this.ToString());
            } catch (Exception ex) {
                throw new DisconnectedException(this.ToString(), ex);
            }
        }

        private async Task ReadPackets() {
            Log.Info($"{this}: {nameof(ReadPackets)}");

            try {
                var buffer = new byte[1024];

                while (!ClosedCancellationToken.IsCancellationRequested) {
                    var count = await _clientStream.ReadAsync(buffer, 0, 1024, ClosedCancellationToken).ConfigureAwait(false);

                    if(count == 0) {
                        throw new DisconnectedException();
                    }

                    var packets = _packetBuilder.AddData(_serializer, buffer, count).ToArray();

                    ProcessReceivedPackets(packets).SignalOnException();
                }
            } catch (ObjectDisposedException) {
                Log.Warn($"{this}: Disconnected");
            } catch (IOException ex) {
                Log.Warn($"{this}: Disconnected", ex);
            } catch (TaskCanceledException) {
                Log.Warn($"{this}: Disconnected. Task canceled.");
            } catch (DisconnectedException ex) when (ex.InnerException != null) {
                Log.Warn($"{this}: Disconnected", ex.InnerException);
            } catch (DisconnectedException) {
                Log.Warn($"{this}: Disconnected");
            } finally {
                Log.Info($"{this}: {nameof(ReadPackets)}: Done reading packets");
            }

            TransitionState(ConnectionState.Closed);
        }

        public override IConnection Clone() => new TcpConnection(this);

        protected override void Dispose(bool disposing) {
            if (!disposing) return;

            _clientStream?.Dispose();
            _client.Dispose();
            base.Dispose(disposing);
        }
    }
}
