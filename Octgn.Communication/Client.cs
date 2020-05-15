using Octgn.Communication.Packets;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class Client : Module
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(typeof(Client));
#pragma warning restore IDE1006 // Naming Styles

        public User User => Connection?.User;
        public IConnection Connection { get; private set; }
        public ISerializer Serializer { get; }

        public bool IsConnected => Status == ConnectionStatus.Connected;

        private readonly IConnectionCreator _connectionCreator;

        public Client(IConnectionCreator connectionCreator, ISerializer serializer) {
            _connectionCreator = connectionCreator ?? throw new ArgumentNullException(nameof(connectionCreator));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public override void Initialize() {
            _connectionCreator.Initialize(this);

            base.Initialize();
        }

        public ConnectionStatus Status {
            get => _status;
            private set {
                // Validate transition
                switch (_status) {
                    case ConnectionStatus.Disconnected:
                        if (value == ConnectionStatus.Disconnected) throw new InvalidOperationException($"Cannot transition from {_status} to {value}");
                        if (value == ConnectionStatus.Connected) throw new InvalidOperationException($"Cannot transition from {_status} to {value}");
                        break;
                    case ConnectionStatus.Connecting:
                        break;
                    case ConnectionStatus.Connected:
                        if (value == ConnectionStatus.Connected) throw new InvalidOperationException($"Cannot transition from {_status} to {value}");
                        if (value == ConnectionStatus.Connecting) throw new InvalidOperationException($"Cannot transition from {_status} to {value}");
                        break;
                    default:
                        throw new NotImplementedException(_status.ToString());
                }

                _status = value;

                switch (_status) {
                    case ConnectionStatus.Disconnected:
                        try {
                            Disconnected?.Invoke(this, new DisconnectedEventArgs { Client = this });
                        } catch (Exception ex) {
                            Signal.Exception(ex, nameof(Status));
                        }
                        break;
                    case ConnectionStatus.Connecting:
                        try {
                            Connecting?.Invoke(this, new ConnectingEventArgs { Client = this });
                        } catch (Exception ex) {
                            Signal.Exception(ex, nameof(Status));
                        }
                        break;
                    case ConnectionStatus.Connected:
                        try {
                            Connected?.Invoke(this, new ConnectedEventArgs { Client = this });
                        } catch (Exception ex) {
                            Signal.Exception(ex, nameof(Status));
                        }
                        break;
                    default:
                        throw new NotImplementedException(_status.ToString());
                }
            }
        }

        private ConnectionStatus _status;

        public event EventHandler<ConnectedEventArgs> Connected;

        public event EventHandler<DisconnectedEventArgs> Disconnected;

        public event EventHandler<ConnectingEventArgs> Connecting;

        private string _host;

        public Task Connect(string host, CancellationToken cancellationToken = default(CancellationToken)) {
            if (Connection != null) throw new InvalidOperationException($"{this}: Cannot call Connect more than once.");
            if (string.IsNullOrWhiteSpace(host)) throw new ArgumentNullException(nameof(host));

            _host = host;

            Initialize();

            return ConnectInternal(cancellationToken);
        }

        private async Task ConnectInternal(CancellationToken cancellationToken = default(CancellationToken)) {
            Log.Info($"{this}: Connecting...");

            try {
                Status = ConnectionStatus.Connecting;

                Connection = _connectionCreator.Create(_host);

                await Connection.Connect(cancellationToken);
                Connection.ConnectionStateChanged += Connection_ConnectionStateChanged;
                Connection.RequestReceived += Connection_RequestReceived;

                // Should do this before an operation that might block
                cancellationToken.ThrowIfCancellationRequested();

                Log.Info($"{this}: Firing connected events...");
                Status = ConnectionStatus.Connected;

                Log.Info($"{this}: Connected");
            } catch {
                if (Connection != null) {
                    Connection.ConnectionStateChanged -= Connection_ConnectionStateChanged;
                    Connection.RequestReceived -= Connection_RequestReceived;
                    Connection = null;
                }
                Status = ConnectionStatus.Disconnected;
                throw;
            }
        }

        private async void Connection_ConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e) {
            try {
                // The underlying connection was closed.
                if (e.NewState == ConnectionState.Closed) {
                    e.Connection.ConnectionStateChanged -= Connection_ConnectionStateChanged;
                    e.Connection.RequestReceived -= Connection_RequestReceived;

                    Log.Warn($"{this}: Disconnected", e.Exception);

                    Status = ConnectionStatus.Disconnected;

                    await ReconnectAsync();
                }
            } catch (Exception ex) {
                Signal.Exception(ex, nameof(Connection_ConnectionStateChanged));
            }
        }

        public static int DefaultReconnectRetryCount { get; set; } = 10;
        public int ReconnectRetryCount { get; set; } = DefaultReconnectRetryCount;
        public static TimeSpan DefaultReconnectRetryDelay = TimeSpan.FromSeconds(5);
        public TimeSpan ReconnectRetryDelay { get; set; } = DefaultReconnectRetryDelay;

        private async Task ReconnectAsync() {
            var currentTry = 0;
            var maxRetryCount = ReconnectRetryCount;

            var reportReconnectFailed = true;

            try {
                Log.Info($"{this}: Reconnecting...");

                for (currentTry = 0; currentTry < maxRetryCount; currentTry++) {
                    if (IsDisposed) {
                        Log.Info($"{this}: {nameof(ReconnectAsync)}: Disposed, stopping reconnect attempt.");
                        reportReconnectFailed = false;
                        break;
                    }

                    try {
                        await Task.Delay(ReconnectRetryDelay, _disposedCancellationTokenSource.Token);

                        if (IsDisposed) {
                            Log.Info($"{this}: {nameof(ReconnectAsync)}: Disposed, stopping reconnect attempt.");
                            reportReconnectFailed = false;
                            break;
                        }

                        Log.Info($"{this}: {nameof(ReconnectAsync)}: Reconnecting...{currentTry}/{maxRetryCount}");

                        await ConnectInternal(_disposedCancellationTokenSource.Token);
                    } catch (TaskCanceledException) {
                        Log.Info($"{this}: {nameof(ReconnectAsync)}: Disposed canceled, stopping reconnect attempt.");
                        reportReconnectFailed = false;
                        break;
                    } catch (Exception ex) {
                        Log.Warn($"{this}: {nameof(ReconnectAsync)}: Error When Reconnecting...Going to try again...", ex);
                        continue;
                    }

                    Log.Info($"{this}: {nameof(ReconnectAsync)}: Reconnected after {currentTry} out of {maxRetryCount} tries");
                    return;
                }
            } finally {
                if (!IsConnected && reportReconnectFailed) {
                    Log.Warn($"{this}: {nameof(ReconnectAsync)}: Failed to reconnect after {currentTry} out of {maxRetryCount} tries");
                }
            }
        }

        public Task<ResponsePacket> Request(RequestPacket request, CancellationToken cancellationToken = default(CancellationToken)) {
            if (!IsConnected) throw new NotConnectedException($"{this}: Could not send the request {request}, the client is not connected.");
            return Connection.Request(request, cancellationToken);
        }

#pragma warning disable RCS1159 // Use EventHandler<T>.
        // This delegate returns a Task, and we need that for our implementation
        public event RequestReceived RequestReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.

        private async Task<object> Connection_RequestReceived(object sender, RequestReceivedEventArgs args) {
            if (sender == null) {
                throw new ArgumentNullException(nameof(sender));
            }

            args.Context.Client = this;

            await OnRequestReceived(this, args);

            if (!args.IsHandled) {

                foreach (var module in GetModules()) {
                    var result = await module.Process(args.Request);
                    if (result.WasProcessed) {
                        args.IsHandled = true;
                        args.Response = result.Result;

                        break;
                    }
                }
            }

            // Copy locally in case it become null
            var eventHandler = RequestReceived;

            if (!args.IsHandled && eventHandler != null) {
                foreach (var handler in eventHandler.GetInvocationList().Cast<RequestReceived>()) {
                    var result = await handler(this, args);

                    if (result != null) {
                        args.Response = result;
                        args.IsHandled = true;
                    }

                    if (args.IsHandled) break;
                }
            }

            return args.Response;
        }

        protected virtual Task OnRequestReceived(object sender, RequestReceivedEventArgs args) {
            return Task.CompletedTask;
        }

        public override string ToString() {
            var userString = User?.ToString() ?? "UNKNOWNUSER";

            return $"{nameof(Client)}: {userString}: {Connection}";
        }

        private readonly CancellationTokenSource _disposedCancellationTokenSource = new CancellationTokenSource();

        protected override void Dispose(bool disposing) {
            if (IsDisposed) return;

            if (disposing) {
                Log.Info($"{this}: Disposed");
                _disposedCancellationTokenSource.Cancel();
                Connection?.Dispose();
                _disposedCancellationTokenSource.Dispose();
            }
            base.Dispose(disposing);
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

    public class ConnectingEventArgs : EventArgs
    {
        public Client Client { get; set; }
    }

    public enum ConnectionStatus { Disconnected, Connecting, Connected }
}
