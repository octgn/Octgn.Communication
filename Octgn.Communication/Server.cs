using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
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
            _serverModules.Add(module);
        }

        public async Task UpdateUserStatus(string userId, string status) {
            var handlerArgs = new UserStatusChangedEventArgs() {
                UserId = userId,
                Status = status
            };
            foreach (var module in _serverModules) {
                await module.UserStatucChanged(this, handlerArgs);
                if (handlerArgs.IsHandled) {
                    break;
                }
            }
        }

        public void Dispose()
        {
            Log.Info("Disposing server");
            Connections.IsClosed = true;
            foreach(var module in _serverModules) {
                (module as IDisposable)?.Dispose();
            }
            _serverModules.Clear();
            Listener.IsEnabled = false;
            Listener.ConnectionCreated -= Listener_ConnectionCreated;
            (Listener as IDisposable)?.Dispose();
        }

        private async void Connection_RequestReceived(object sender, RequestPacketReceivedEventArgs args)
        {
            async Task RespondPacket(ResponsePacket packet)
            {
                try {
                    await args.Connection.Response(packet);
                } catch (Exception ex) {
                    var errorMessage = $"Failed to send response packet {packet} to {args.Connection}";
                    Signal.Exception(ex, errorMessage);
                    Log.Error(errorMessage, ex);
                }
            }
            async Task Respond(object response)
            {
                var responsePacket = new ResponsePacket(args.Packet, response);

                try {
                    await args.Connection.Response(responsePacket);
                } catch (Exception ex) {
                    var errorMessage = $"Failed to send response packet {responsePacket} to {args.Connection}";
                    Signal.Exception(ex, errorMessage);
                    Log.Error(errorMessage, ex);
                }
            }
            try {
                Log.Info($"Handling {args.Packet}");
                try {
                    if (!string.IsNullOrWhiteSpace(args.Packet.Destination)) {
                        var fromUser = ConnectionProvider.GetUserId(args.Connection);

                        args.Packet.Origin = fromUser;

                        var toUserConnections = ConnectionProvider.GetConnections(args.Packet.Destination);

                        var newPacket = args.Packet;
                        var sendCount = 0;
                        ResponsePacket receiverResponse = null;

                        foreach (var connection in toUserConnections) {
                            try {
                                Log.Info($"Sending {newPacket} to {connection}");

                                receiverResponse = await connection.Request(newPacket);
                                sendCount++;
                            } catch (Exception ex) {
                                Log.Warn(ex);
                            }
                        }
                        if (sendCount < 1) {
                            Log.Warn($"Unable to deliver message to user {args.Packet.Destination}, they are offline");
                            throw new ErrorResponseException(ErrorResponseCodes.UserOffline, $"Unable to deliver message to user {args.Packet.Destination}, they are offline", false);
                        } else {
                            Log.Info($"Sent {args.Packet}");
                            await Respond(receiverResponse.Data);
                        }
                    } else {
                        if (args.Packet.Name == nameof(AuthenticationRequestPacket)) {
                            var result = await _authenticationHandler.Authenticate(this, args.Connection, (AuthenticationRequestPacket)args.Packet);

                            await ConnectionProvider.AddConnection(args.Connection, result.UserId);

                            await RespondPacket(new ResponsePacket(args.Packet, result));

                            if(!result.Successful)
                                args.Connection.IsClosed = true;
                        } else {
                            ResponsePacket response = await HandleRequest(args);

                            await RespondPacket(response);
                        }
                    }
                } catch (ErrorResponseException ex) {
                    var err = new ErrorResponseData(ex.Code, ex.Message, ex.IsCritical);
                    await RespondPacket(new ResponsePacket(args.Packet, err));
                    if (ex.IsCritical) 
                        args.Connection.IsClosed = true;
                } catch (UnauthorizedAccessException ex) {
                    await RespondPacket(new ResponsePacket(args.Packet, new ErrorResponseData(ErrorResponseCodes.UnauthorizedRequest, ex.Message, true)));
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
                throw new NotImplementedException($"Packet {args.Packet} not expected.");

            return response;
        }
    }
}
