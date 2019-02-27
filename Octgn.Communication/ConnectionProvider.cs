using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class ConnectionProvider : IConnectionProvider {
        private IConnection[] _connections;
        private readonly object _connectionLocker = new object();

        public ConnectionProvider() {
            _connections = new IConnection[0];
        }

        public virtual Task AddConnection(IConnection connection) {
            if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));

            lock (_connectionLocker) {
                if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));

                var cons = _connections.ToList();
                cons.Add(connection);
                _connections = cons.ToArray();

                connection.ConnectionStateChanged += Connection_ConnectionStateChanged;
            }
            return Task.CompletedTask;
        }

        private void Connection_ConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e) {
            switch (e.NewState) {
                case ConnectionState.Closed:
                    RemoveConnection(e.Connection);
                    break;
            }

            ConnectionStateChanged?.Invoke(this, e);
        }

        private void RemoveConnection(IConnection connection) {
            connection.ConnectionStateChanged -= Connection_ConnectionStateChanged;

            if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));

            lock (_connectionLocker) {
                if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));

                _connections = _connections.Where(con => !con.Equals(connection)).ToArray();
                connection.Dispose();
            }
        }

        public virtual IEnumerable<IConnection> GetConnections() {
            if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));

            return _connections;
        }

        public IEnumerable<IConnection> GetConnections(string destination, bool isConnected) {
            IEnumerable<IConnection> query = null;

            if(destination == "everyone") {
                query = _connections;
            } else {
                query = _connections.Where(con => con.User.Id.Equals(destination, StringComparison.InvariantCultureIgnoreCase));
            }

            if (isConnected)
                query = query.Where(con => con.State == ConnectionState.Connected);

            return query;
        }

        public virtual void Initialize(Server server) {
            if (_disposedValue) throw new ObjectDisposedException(nameof(ConnectionProvider));
        }

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    lock (_connectionLocker) {
                        foreach(var con in _connections) {
                            con.ConnectionStateChanged -= Connection_ConnectionStateChanged;
                            con.Dispose();
                        }
                        _connections = null;
                    }
                }

                _disposedValue = true;
            }
        }

        public void Dispose() {
            Dispose(true);
        }
        #endregion
    }
}
