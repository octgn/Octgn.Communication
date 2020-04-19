using Octgn.Communication.Packets;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class Server : Module
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(Server));
#pragma warning restore IDE1006 // Naming Styles

        public IConnectionListener Listener { get; }
        public IConnectionProvider ConnectionProvider { get; }
        public ISerializer Serializer { get; }

        public Server(IConnectionListener listener, ISerializer serializer) {
            Listener = listener ?? throw new ArgumentNullException(nameof(listener));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));

            ConnectionProvider = new ConnectionProvider();

            Listener.ConnectionCreated += Listener_ConnectionCreated;
        }

        public override void Initialize() {
            Listener.Initialize(this);

            ConnectionProvider.Initialize(this);

            Listener.IsEnabled = true;

            base.Initialize();
        }

        private void Listener_ConnectionCreated(object sender, ConnectionCreatedEventArgs args) {
            try {
                Log.Info($"Connection Created {args.Connection.ConnectionId}");
                args.Connection.RequestReceived += Connection_RequestReceived;
                args.Connection.PacketReceived += Connection_PacketReceived;
                ConnectionProvider.AddConnection(args.Connection);

                if (args.Connection is ConnectionBase cb) {
                    cb.StartHandshaking();
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        protected override void Dispose(bool disposing) {
            if (IsDisposed) return;

            if (disposing) {
                Log.Info("Disposing server");

                Listener.IsEnabled = false;
                Listener.ConnectionCreated -= Listener_ConnectionCreated;
                (Listener as IDisposable)?.Dispose();

                ConnectionProvider.Dispose();
            }
            base.Dispose(disposing);
        }

        public async Task<ResponsePacket> Request(RequestPacket request, string destination = null) {
            if (this.IsDisposed) throw new InvalidOperationException($"Could not send the request {request}, the server is disposed.");

            var sendCount = 0;
            ResponsePacket receiverResponse = null;

            request.Destination = destination;

            var isRouted = !string.IsNullOrWhiteSpace(request.Destination);

            var connectionQuery = ConnectionProvider.GetConnections()
                .Where(con => con.State == ConnectionState.Connected);

            if (isRouted) {
                connectionQuery = connectionQuery.Where(con => con.User.Id.Equals(destination, StringComparison.InvariantCultureIgnoreCase));
            }

            var connections = connectionQuery.ToArray();

            Log.Info($"Sending {request} to {connections.Length} connections: {string.Join(",", connections.Take(10))}");

            foreach (var connection in connections) {
                try {
                    receiverResponse = await connection.Request(request);

                    sendCount++;
                } catch (Exception ex) when (!(ex is ErrorResponseException)) {
                    Log.Warn(ex);
                }
            }

            if (isRouted && sendCount < 1) {
                Log.Warn($"Unable to deliver packet to user {destination}, they are offline or the destination is invalid.");
                throw new ErrorResponseException(ErrorResponseCodes.UserOffline, $"Unable to deliver packet to user {destination}, they are offline of the destination is invalid.", false);
            }

            Log.Info($"Sent {request} to {sendCount} clients");

            if (isRouted && receiverResponse == null && request.Flags.HasFlag(PacketFlag.AckRequired))
                throw new ErrorResponseException(ErrorResponseCodes.UnhandledRequest, $"Packet {request} routed to {destination}, but they didn't handle the request.", false);

            return receiverResponse;
        }

        private async Task Connection_PacketReceived(object sender, PacketReceivedEventArgs args) {
            if (sender == null) {
                throw new ArgumentNullException(nameof(sender));
            }

            Interlocked.Increment(ref _packetCount);

            if (typeof(RequestPacket).IsAssignableFrom(args.Packet.GetPacketType())) {
                if (!string.IsNullOrWhiteSpace(args.Packet.Destination)) {
                    var sendCount = 0;

                    var connections = ConnectionProvider.GetConnections(args.Packet.Destination, true).ToArray();

                    Log.Info($"Sending {args.Packet} to {connections.Length} connections: {string.Join(",", connections.Take(10))}");

                    try {

                        foreach (var connection in connections) {
                            try {
                                var response = await connection.Send(args.Packet);

                                if (args.Packet.Flags.HasFlag(PacketFlag.AckRequired)) {

                                    if (response == null) {
                                        throw new ErrorResponseException(ErrorResponseCodes.UnhandledRequest, $"Packet {args.Packet} routed to {args.Packet.Destination}, but they didn't handle the request.", false);
                                    }

                                    var responsePacket = (ResponsePacket)response;

                                    await args.Connection.Respond(args.PacketId, responsePacket);
                                }

                                sendCount++;
                            } catch (Exception ex) when (!(ex is ErrorResponseException)) {
                                Log.Warn(ex);
                            }
                        }

                        if (sendCount < 1) {
                            Log.Warn($"Unable to deliver packet to user {args.Packet.Destination}, they are offline or the destination is invalid.");
                            throw new ErrorResponseException(ErrorResponseCodes.UserOffline, $"Unable to deliver packet to user {args.Packet.Destination}, they are offline of the destination is invalid.", false);
                        }
                    } catch (ErrorResponseException ex) {
                        var err = new ErrorResponseData(ex.Code, ex.Message, ex.IsCritical);
                        await args.Connection.Respond(args.PacketId, new ResponsePacket(err));
                    }

                    Log.Info($"Sent {args.Packet} to {sendCount} clients");

                    args.IsHandled = true;
                }
            }
        }

        public long PacketCount { get => _packetCount; }
        private long _packetCount;

        private async Task<object> Connection_RequestReceived(object sender, RequestReceivedEventArgs args) {
            if (sender == null) {
                throw new ArgumentNullException(nameof(sender));
            }

            args.Context.Server = this;

            Log.Info($"{args.Context}: Handling {args.Request}");
            try {
                args.Request.Origin = args.Context.Connection.User;

                foreach (var module in GetModules()) {
                    var result = await module.Process(args.Request);
                    if (result.WasProcessed) {
                        args.IsHandled = true;
                        args.Response = result.Result;

                        break;
                    }
                }
            } catch (ErrorResponseException ex) {
                var err = new ErrorResponseData(ex.Code, ex.Message, ex.IsCritical);
                args.Response = err;
            } catch (Exception ex) {
                Signal.Exception(ex);

                var err = new ErrorResponseData(ErrorResponseCodes.UnhandledServerError, "UnhandledServerError", true);
                args.Response = err;
            }
            return args.Response;
        }
    }
}
