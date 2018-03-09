using System;
using System.Net;
using System.Net.Sockets;

namespace Octgn.Communication
{
    public class TcpListener : IConnectionListener, IDisposable
    {
#pragma warning disable IDE1006 // Naming Styles
        private readonly static ILogger Log = LoggerFactory.Create(typeof(TcpListener));
#pragma warning restore IDE1006 // Naming Styles

        public IPEndPoint EndPoint { get; }

        private readonly System.Net.Sockets.TcpListener _listener;
        private readonly ISerializer _serializer;
        private readonly IHandshaker _handshaker;

        public TcpListener(IPEndPoint endpoint, ISerializer serializer, IHandshaker handshaker) {
            EndPoint = endpoint;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));
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
        }

        private bool _isEnabled;

        private async void ListenForConnectionAsync() {
            try {
                while (_isEnabled) {
                    TcpClient result = null;
                    try {
                        result = await _listener.AcceptTcpClientAsync();
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

        private IConnection CreateConnection(TcpClient client) => new TcpConnection(client, _serializer, _handshaker);

        public event ConnectionCreated ConnectionCreated;

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    // TODO: dispose managed state (managed objects).
                    _listener.Stop();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TcpListener() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
