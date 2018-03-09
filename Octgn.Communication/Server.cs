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

        public Server(IConnectionListener listener, IConnectionProvider connectionProvider) {
            Listener = listener ?? throw new ArgumentNullException(nameof(listener));
            ConnectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));

            Listener.ConnectionCreated += Listener_ConnectionCreated;

            // Must be at the end
            ConnectionProvider.Initialize(this);
        }

        public override void Initialize() {
            Listener.IsEnabled = true;

            base.Initialize();
        }

        private void Listener_ConnectionCreated(object sender, ConnectionCreatedEventArgs args) {
            try {
                Log.Info($"Connection Created {args.Connection.ConnectionId}");
                args.Connection.RequestReceived += Connection_RequestReceived;
                if (args.Connection is ConnectionBase connectionBase) {
                    connectionBase.Initialize(this);
                }
                ConnectionProvider.AddConnection(args.Connection);
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

            var connectionQuery = ConnectionProvider.GetConnections()
                .Where(con => con.State == ConnectionState.Connected);

            if (!string.IsNullOrWhiteSpace(destination)) {
                connectionQuery = connectionQuery.Where(con => con.User.Id.Equals(destination, StringComparison.InvariantCultureIgnoreCase));
            }

            var connections = connectionQuery.ToArray();

            Log.Info($"Sending {request} to {connections.Length} connections: {string.Join(",", connections.Take(10))}");

            foreach (var connection in connections) {
                try {
                    var newRequest = new RequestPacket(request);
                    receiverResponse = await connection.Request(newRequest);

                    sendCount++;
                } catch (Exception ex) when (!(ex is ErrorResponseException)) {
                    Log.Warn(ex);
                }
            }

            if (sendCount < 1) {
                Log.Warn($"Unable to deliver packet to user {destination}, they are offline or the destination is invalid.");
                throw new ErrorResponseException(ErrorResponseCodes.UserOffline, $"Unable to deliver packet to user {destination}, they are offline of the destination is invalid.", false);
            }

            Log.Info($"Sent {request} to {sendCount} clients");

            if (receiverResponse == null && request.RequiresAck)
                throw new ErrorResponseException(ErrorResponseCodes.UnhandledRequest, $"Packet {request} routed to {destination}, but they didn't handle the request.", false);

            return receiverResponse;
        }

        public int RequestCount { get => _requestCount; }
        private int _requestCount;

        private async Task<ResponsePacket> Connection_RequestReceived(object sender, RequestReceivedEventArgs args) {
            if (sender == null) {
                throw new ArgumentNullException(nameof(sender));
            }

            Interlocked.Increment(ref _requestCount);

            args.Context.Server = this;

            Log.Info($"{args.Context}: Handling {args.Request}");
            try {
                args.Request.Origin = args.Context.Connection.User;

                if (!string.IsNullOrWhiteSpace(args.Request.Destination)) {
                    args.Response = await Request(args.Request, args.Request.Destination);
                    args.Response.RequestPacketId = (ulong)args.Request.Id;
                } else {
                    foreach (var module in GetModules()) {
                        var result = await module.Process(args.Request);
                        if (result.WasProcessed) {
                            args.IsHandled = true;
                            args.Response = (result.Result is ResponsePacket resultResponse)
                                ? resultResponse : new ResponsePacket(args.Request, result.Result);

                            break;
                        }
                    }
                }
            } catch (ErrorResponseException ex) {
                var err = new ErrorResponseData(ex.Code, ex.Message, ex.IsCritical);
                args.Response = new ResponsePacket(args.Request, err);
            } catch (Exception ex) {
                Signal.Exception(ex);

                var err = new ErrorResponseData(ErrorResponseCodes.UnhandledServerError, "UnhandledServerError", true);
                args.Response = new ResponsePacket(args.Request, err);
            }
            return args.Response;
        }
    }
}
