using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class Client : IDisposable
    {
        private static ILogger Log = LoggerFactory.Create(typeof(Client));

        public string UserId { get; set; }
        public IConnection Connection { get; private set; }
        private readonly object L_CONNECTION = new object();

        public bool IsConnected { get; private set; }

        public event Connected Connected;
        protected void FireConnectedEvent()
        {
            try {
                Connected?.Invoke(this, new ConnectedEventArgs { Client = this });
            } catch (Exception ex) {
                Signal.Exception(ex, nameof(FireConnectedEvent));
            }
        }
        public event Disconnected Disconnected;
        protected void FireDisconnectedEvent()
        {
            try {
                Disconnected?.Invoke(this, new DisconnectedEventArgs { Client = this });
            } catch (Exception ex) {
                Signal.Exception(ex, nameof(FireDisconnectedEvent));
            }
        }

        public ISerializer Serializer { get; }

        public IAuthenticator Authenticator { get; }

        public Client(IConnection connection, ISerializer serializer, IAuthenticator authenticator) {
            Connection = connection ?? throw new ArgumentNullException(nameof(connection));
            Connection.Serializer = Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            Authenticator = authenticator ?? throw new ArgumentNullException(nameof(authenticator));
        }

        private bool _connected;
        public Task Connect() {
            if (_connected) throw new InvalidOperationException("Cannot call Connect more than once.");

            return ConnectInternal();
        }

        private bool _authenticating = false;
        private async Task ConnectInternal() {
            await Connection.Connect();
            Connection.ConnectionClosed += Connection_ConnectionClosed;
            Connection.RequestReceived += Connection_RequestReceived;

            AuthenticationResult result = null;

            try {
                _authenticating = true;
                result = await Authenticator.Authenticate(this, Connection);

                if (!result.Successful) {
                    throw new AuthenticationException(result.ErrorCode);
                }
            } finally {
                _authenticating = false;
            }

            this.UserId = result.UserId;

            _connected = true;
            IsConnected = true;

            FireConnectedEvent();
        }

        private async void Connection_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            try
            {
                args.Connection.ConnectionClosed -= Connection_ConnectionClosed;
                Log.Warn("Disconnected", args.Exception);
                IsConnected = false;
                FireDisconnectedEvent();
                if (_disposed)
                {
                    Log.Info($"Client: {args.Connection}: {UserId}: Disposed, not going to try and reconnect");
                    return;
                }

                await ReconnectAsync();
            } catch (Exception ex)
            {
                Signal.Exception(ex, nameof(Connection_ConnectionClosed));
            }
        }

        public const int ReconnectRetryCount = 10;

        private async Task ReconnectAsync() {
            var currentTry = 0;
            var maxRetryCount = ReconnectRetryCount;
            try {
                for(currentTry = 0; currentTry < maxRetryCount; currentTry++)
                {
                    if (_disposed) {
                        throw new ObjectDisposedException(nameof(Client));
                    }

                    if (!Connection.IsClosed)
                        throw new InvalidOperationException($"{nameof(ReconnectAsync)}: Can't reconnect if the {nameof(Connection)} is not closed.");

                    Connection = Connection.Clone();

                    if (Connection.IsClosed)
                        throw new InvalidOperationException($"{nameof(ReconnectAsync)}: Can't reconnect if the cloned {nameof(Connection)} is already closed.");

                    Connection.Serializer = this.Serializer;

                    Log.Info($"{nameof(ReconnectAsync)}: Reconnecting...{currentTry}/{maxRetryCount}");

                    try {
                        await ConnectInternal();
                    } catch (Exception ex) {
                        Log.Warn($"{nameof(ReconnectAsync)}: Error When Reconnecting...Going to try again...", ex);
                        await Task.Delay(5000);
                        continue;
                    }

                    Log.Info($"{nameof(ReconnectAsync)}: Reconnected after {currentTry} out of {maxRetryCount} tries");
                    return;
                }
            } finally {
                if(!IsConnected)
                    Log.Warn($"{nameof(ReconnectAsync)}: Failed to reconnect after {currentTry} out of {maxRetryCount} tries");
            }
        }

        private readonly Dictionary<Type, IClientModule> _clientModules = new Dictionary<Type, IClientModule>();
        public void Attach(IClientModule module) {
            _clientModules.Add(module.GetType(), module);
        }

        public T GetModule<T>() where T : IClientModule{
            return (T)_clientModules[typeof(T)];
        }

        private bool _disposed;
        public void Dispose() {
            _disposed = true;
            Log.Info($"Client: {Connection}: {UserId}: Disposed");
            foreach(var moduleKVP in _clientModules) {
                var module = moduleKVP.Value;

                (module as IDisposable)?.Dispose();
            }
            _clientModules.Clear();
            if (Connection != null) {
                Connection.IsClosed = true;
                (Connection as IDisposable)?.Dispose();
            }
        }

        public Task<ResponsePacket> Request(RequestPacket request) {
            if (!IsConnected && !_authenticating) throw new NotConnectedException($"Could not send the request {request}, the client is not connected.");
            return Connection.Request(request);
        }

        public event RequestReceived RequestReceived;
        protected ResponsePacket FireRequestReceived(RequestPacket packet)
        {
            var requestArgs = new RequestReceivedEventArgs {
                Client = this,
                Request = packet
            };

            try {
                RequestReceived?.Invoke(this, requestArgs);
            } catch (Exception ex) {
                Signal.Exception(ex, nameof(FireRequestReceived));
            }

            return requestArgs.Response;
        }

        private async void Connection_RequestReceived(object sender, RequestPacketReceivedEventArgs args) {
            try {
                ResponsePacket response = null;

                var handlerArgs = new HandleRequestEventArgs(args);
                foreach (var handler in _clientModules.Values) {
                    await handler.HandleRequest(this, handlerArgs);
                    if (handlerArgs.IsHandled || handlerArgs.Response != null) {
                        response = handlerArgs.Response;
                        break;
                    }
                }

                if(response == null) {
                    response = FireRequestReceived(args.Packet);
                }

                if (response == null)
                    throw new NotImplementedException($"Packet {args.Packet} not expected.");

                try {
                    await args.Connection.Response(response);
                } catch (TimeoutException) {
                    Log.Error($"Failed to send response packet {args.Packet} to {args.Connection}");
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }
    }

    public delegate void RequestReceived(object sender, RequestReceivedEventArgs args);

    public class RequestReceivedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public RequestPacket Request { get; set; }
        public ResponsePacket Response { get; set; }
    }

    public delegate void Connected(object sender, ConnectedEventArgs args);

    public class ConnectedEventArgs : EventArgs
    {
        public Client Client { get; set; }
    }

    public delegate void Disconnected(object sender, DisconnectedEventArgs args);

    public class DisconnectedEventArgs : EventArgs
    {
        public Client Client { get; set; }
    }
}
