using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class Server : IDisposable
    {
        private static ILogger Log = LoggerFactory.Create(nameof(Server));

        public bool IsEnabled {
            get => _isEnabled = Listener?.IsEnabled ?? false;
            set {
                if (_isEnabled == value) return;
                _isEnabled = value;
                if (_isEnabled) {
                    Log.Info($"Enabled Server");
                } else {
                    Log.Info($"Disabled Server");
                }

                var list = Listener;

                if (list != null)
                    list.IsEnabled = value;
            }
        }
        private bool _isEnabled;

        public ConcurrentConnectionCollection Connections { get; } = new ConcurrentConnectionCollection();

        public IConnectionListener Listener { get; }
        public IConnectionProvider ConnectionProvider { get; }
        public ISerializer Serializer { get; }

        public Server(IConnectionListener listener, IConnectionProvider connectionProvider, ISerializer serializer, IAuthenticationHandler authenticationHandler)
        {
            Listener = listener ?? throw new ArgumentNullException(nameof(listener));
            ConnectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _authenticationHandler = authenticationHandler ?? throw new ArgumentNullException(nameof(authenticationHandler));

            Listener.ConnectionCreated += Listener_ConnectionCreated;

            // Must be at the end
            ConnectionProvider.Initialize(this);
        }

        private void Listener_ConnectionCreated(object sender, ConnectionCreatedEventArgs args)
        {
            try {
                Log.Info($"Connection Created {args.Connection.ConnectionId}");
                Connections.Add(args.Connection);
                args.Connection.Serializer = Serializer;
                args.Connection.RequestReceived += Connection_RequestReceived;
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private readonly List<IServerModule> _serverModules = new List<IServerModule>();
        private readonly IAuthenticationHandler _authenticationHandler;

        public void Attach(IServerModule module) {
            lock (_serverModules) {
                _serverModules.Add(module);
            }
        }

        public async Task UpdateUserStatus(string userId, string status) {
            VerifyNotDisposed();

            var handlerArgs = new UserStatusChangedEventArgs() {
                UserId = userId,
                Status = status
            };
            IServerModule[] serverModules = null;

            lock(_serverModules)
                serverModules = _serverModules.ToArray();

            foreach (var module in serverModules) {
                try {
                    VerifyNotDisposed();
                    await module.UserStatucChanged(this, handlerArgs);
                    if (handlerArgs.IsHandled) {
                        break;
                    }
                } catch (Exception ex) {
                    Signal.Exception(ex);
                }
            }
        }

        private int _disposeCallCount;
        protected bool IsDisposed => _disposeCallCount > 0;

        private void VerifyNotDisposed() {
            if (IsDisposed) throw new InvalidOperationException($"{nameof(Server)} is Disposed");
        }

        public void Dispose()
        {
            if (Interlocked.CompareExchange(ref _disposeCallCount, 1, 0) > 0) throw new InvalidOperationException($"{nameof(Server)} is Disposed");

            Log.Info("Disposing server");
            Connections.IsClosed = true;

            IServerModule[] serverModules = null;

            lock (_serverModules) {
                serverModules = _serverModules.ToArray();
                _serverModules.Clear();
            }

            foreach(var module in serverModules) {
                (module as IDisposable)?.Dispose();
            }
            Listener.IsEnabled = false;
            Listener.ConnectionCreated -= Listener_ConnectionCreated;
            (Listener as IDisposable)?.Dispose();
        }

        public async Task<ResponsePacket> Request(RequestPacket request, string destination = null) {
            if (this.IsDisposed) throw new InvalidOperationException($"Could not send the request {request}, the server is disposed.");
            if (!this.IsEnabled) throw new NotConnectedException($"Could not send the request {request}, the server is not enabled.");

            var sendCount = 0;
            ResponsePacket receiverResponse = null;

            request.Destination = destination;

            var connections = !string.IsNullOrWhiteSpace(destination)
                ? ConnectionProvider.GetConnections(destination)
                : ConnectionProvider.GetConnections();

            foreach (var connection in connections) {
                try {
                    Log.Info($"Sending {request} to {connection}");

                    receiverResponse = await connection.Request(request);
                    sendCount++;
                } catch (Exception ex) when (!(ex is ErrorResponseException)) {
                    Log.Warn(ex);
                }
            }
            if (sendCount < 1) {
                Log.Warn($"Unable to deliver message to user {destination}, they are offline or the destination is invalid.");
                throw new ErrorResponseException(ErrorResponseCodes.UserOffline, $"Unable to deliver message to user {destination}, they are offline of the destination is invalid.", false);
            } 

            Log.Info($"Sent {request} to {sendCount} clients");

            if(receiverResponse == null)
                throw new ErrorResponseException(ErrorResponseCodes.UnhandledRequest, $"Packet {request} routed to {destination}, but they didn't handle the request.", false);

            return receiverResponse;
        }

        private async void Connection_RequestReceived(object sender, RequestPacketReceivedEventArgs args)
        {
            async Task RespondPacket(ResponsePacket packet)
            {
                try {
                    await args.Connection.Response(packet);
                } catch (Exception ex) {
                    var errorMessage = $"{nameof(RespondPacket)}: Failed to send response packet {packet} to {args.Connection}";
                    Signal.Exception(ex, errorMessage);
                    Log.Error(errorMessage, ex);
                }
            }
            try {
                Log.Info($"Handling {args.Packet}");
                try {
                    if (args.Packet.Name == nameof(AuthenticationRequestPacket)) {
                        var result = await _authenticationHandler.Authenticate(this, args.Connection, (AuthenticationRequestPacket)args.Packet);

                        await ConnectionProvider.AddConnection(args.Connection, result.UserId);

                        await RespondPacket(new ResponsePacket(args.Packet, result));

                        if (!result.Successful)
                            args.Connection.IsClosed = true;
                        return;
                    }

                    args.Packet.Origin = ConnectionProvider.GetUserId(args.Connection);

                    if (!string.IsNullOrWhiteSpace(args.Packet.Destination)) {
                        var response = await Request(args.Packet, args.Packet.Destination);
                        await RespondPacket(response);
                    } else {
                        ResponsePacket response = await HandleRequest(args);

                        await RespondPacket(response);
                    }
                } catch (ErrorResponseException ex) {
                    var err = new ErrorResponseData(ex.Code, ex.Message, ex.IsCritical);
                    await RespondPacket(new ResponsePacket(args.Packet, err));
                    if (ex.IsCritical) 
                        args.Connection.IsClosed = true;
                } catch (Exception ex) {
                    Signal.Exception(ex);

                    var err = new ErrorResponseData(ErrorResponseCodes.UnhandledServerError, "", true);
                    await RespondPacket(new ResponsePacket(args.Packet, new ErrorResponseData(ErrorResponseCodes.UnhandledServerError, ex.Message, true)));

                    args.Connection.IsClosed = true;
                }
            } catch (Exception ex) {
                // This also catches any exceptions raised by the exception handlers above because they try and send the error back to the user.
                Signal.Exception(ex);
                args.Connection.IsClosed = true;
            }
        }

        protected async Task<ResponsePacket> HandleRequest(RequestPacketReceivedEventArgs args) {
            ResponsePacket response = null;
            var handlerArgs = new HandleRequestEventArgs(args);
            foreach (var module in _serverModules) {
                await module.HandleRequest(this, handlerArgs);
                if (handlerArgs.IsHandled || handlerArgs.Response != null) {
                    response = handlerArgs.Response;
                    break;
                }
            }

            if (response == null)
                throw new ErrorResponseException(ErrorResponseCodes.UnhandledRequest, $"Packet {args.Packet} not expected.", false);

            return response;
        }
    }
}
