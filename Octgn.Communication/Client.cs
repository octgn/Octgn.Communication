using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class Client : IDisposable
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(typeof(Client));
#pragma warning restore IDE1006 // Naming Styles

        public string UserId { get; set; }
        public IConnection Connection { get; private set; }

        public bool IsConnected { get; private set; }

        public event EventHandler<ConnectedEventArgs> Connected;
        protected void FireConnectedEvent()
        {
            try {
                Connected?.Invoke(this, new ConnectedEventArgs { Client = this });
            } catch (Exception ex) {
                Signal.Exception(ex, nameof(FireConnectedEvent));
            }
        }

        public event EventHandler<DisconnectedEventArgs> Disconnected;
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
            if (_connected) throw new InvalidOperationException($"{this}: Cannot call Connect more than once.");

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
                args.Connection.ConnectionClosed += Connection_ConnectionClosed;
                Log.Warn("{this}: Disconnected", args.Exception);
                IsConnected = false;
                FireDisconnectedEvent();
                if (_disposed)
                {
                    Log.Info($"{this}: {args.Connection}: {UserId}: Disposed, not going to try and reconnect");
                    return;
                }

                await ReconnectAsync();
            } catch (Exception ex)
            {
                Signal.Exception(ex, nameof(Connection_ConnectionClosed));
            }
        }

        public const int ReconnectRetryCount = 10;
        public static TimeSpan DefaultReconnectRetryDelay = TimeSpan.FromSeconds(5);
        public TimeSpan ReconnectRetryDelay { get; set; } = DefaultReconnectRetryDelay;

        private async Task ReconnectAsync() {
            var currentTry = 0;
            const int maxRetryCount = ReconnectRetryCount;
            try {
                for(currentTry = 0; currentTry < maxRetryCount; currentTry++)
                {
                    if (_disposed) break;

                    if (!Connection.IsClosed)
                        throw new InvalidOperationException($"{this}: {nameof(ReconnectAsync)}: Can't reconnect if the {nameof(Connection)} is not closed.");

                    Connection.ConnectionClosed -= Connection_ConnectionClosed;
                    Connection.RequestReceived -= Connection_RequestReceived;

                    Connection = Connection.Clone();

                    if (Connection.IsClosed)
                        throw new InvalidOperationException($"{this}: {nameof(ReconnectAsync)}: Can't reconnect if the cloned {nameof(Connection)} is already closed.");

                    Connection.Serializer = this.Serializer;

                    Log.Info($"{this}: {nameof(ReconnectAsync)}: Reconnecting...{currentTry}/{maxRetryCount}");

                    try {
                        await Task.Delay(ReconnectRetryDelay);
                        await ConnectInternal();
                    } catch (Exception ex) {
                        Log.Warn($"{this}: {nameof(ReconnectAsync)}: Error When Reconnecting...Going to try again...", ex);
                        continue;
                    }

                    Log.Info($"{this}: {nameof(ReconnectAsync)}: Reconnected after {currentTry} out of {maxRetryCount} tries");
                    return;
                }
            } finally {
                if(!IsConnected)
                    Log.Warn($"{this}: {nameof(ReconnectAsync)}: Failed to reconnect after {currentTry} out of {maxRetryCount} tries");
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
            Log.Info($"{this}: Disposed");
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
            if (!IsConnected && !_authenticating) throw new NotConnectedException($"{this}: Could not send the request {request}, the client is not connected.");
            return Connection.Request(request);
        }

#pragma warning disable RCS1159 // Use EventHandler<T>.
        public event RequestPacketReceived RequestReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.

        private async Task Connection_RequestReceived(object sender, RequestPacketReceivedEventArgs args) {
            if (sender == null) {
                throw new ArgumentNullException(nameof(sender));
            }

            try {
                args.Client = this;

                foreach (var handler in _clientModules.Values) {
                    await handler.HandleRequest(this, args);
                    if (args.IsHandled)
                        break;
                }

                if(!args.IsHandled) {
                    RequestReceived?.Invoke(this, args);
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        public override string ToString() {
            return $"{nameof(Client)} {this.UserId}: {Connection}";
        }
    }

    public class ConnectedEventArgs : EventArgs
    {
        public Client Client { get; set; }
    }

    public class DisconnectedEventArgs : EventArgs
    {
        public Client Client { get; set; }
    }
}
