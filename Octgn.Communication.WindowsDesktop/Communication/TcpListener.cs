using System;
using System.Net;
using System.Net.Sockets;

namespace Octgn.Communication
{
    public class TcpListener : IConnectionListener, IDisposable
    {
        private readonly static ILogger Log = LoggerFactory.Create(typeof(TcpListener));

        public IPEndPoint EndPoint { get; }

        private readonly System.Net.Sockets.TcpListener _listener;

        public TcpListener(IPEndPoint endpoint) {
            EndPoint = endpoint;
            _listener = new System.Net.Sockets.TcpListener(endpoint);
        }

        public bool IsEnabled {
            get => _isEnabled;
            set {
                if (_isEnabled == value) return;
                _isEnabled = value;

                if (_isEnabled) {
                    _listener.Start();
                    ListenForConnectionAsync();
                } else {
                    _listener.Stop();
                }
            }
        } private bool _isEnabled;

        private async void ListenForConnectionAsync() {
            try {
                while (_isEnabled) {
                    TcpClient result = null;
                    try {
                        result = await _listener.AcceptTcpClientAsync()
                            .ConfigureAwait(false);

                    } catch (ObjectDisposedException) {
                        break;
                    }

                    // We don't expect this to ever happen, just a safeguard.
                    // It shouldn't even be possible
                    if (result == null) throw new InvalidOperationException("Listener returned a null result?");

                    try {
                        ConnectionCreated?.Invoke(this, new ConnectionCreatedEventArgs {
                            Connection = CreateConnection(result)
                        });
                    } catch (Exception ex) {
                        Log.Error("Error invoking ConnectionCreated", ex);
                    }
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private IConnection CreateConnection(TcpClient client)
        {
            return ConnectionCreator(client);
        }

        internal Func<TcpClient,IConnection> ConnectionCreator = (client) => new TcpConnection(client);

        public event ConnectionCreated ConnectionCreated;

        public void Dispose() {
            _listener.Stop();
        }
    }
}
