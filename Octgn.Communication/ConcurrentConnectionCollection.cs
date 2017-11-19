using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using System.Threading;

namespace Octgn.Communication
{
    public class ConcurrentConnectionCollection : IConnection
    {
#pragma warning disable RCS1079 // Throwing of new NotImplementedException.
        // This doesn't need a serializer, each connection has it's own.
        public ISerializer Serializer {
            get => throw new NotImplementedException();
            set => throw new NotImplementedException();
        }
#pragma warning restore RCS1079 // Throwing of new NotImplementedException.

        public ConcurrentConnectionCollection() {
            _collection = new HashSet<IConnection>();
        }

        public ConcurrentConnectionCollection(IConnection connection) {
            _collection = new HashSet<IConnection>();
            Add(connection ?? throw new ArgumentNullException(nameof(connection)));
        }

        public IEnumerable<IConnection> GetConnections() {
            return _collection.ToArray();
        }

        private readonly ICollection<IConnection> _collection;

        public int Count => _collection.Count;

        public void Add(IConnection item) {
            if (item == this) throw new InvalidOperationException("Can't add self");
            lock (_collection) {
                item.ConnectionClosed += Item_ConnectionClosed;
                item.RequestReceived += Item_RequestReceived;
                _collection.Add(item);
            }
        }

        private Task Item_RequestReceived(object sender, RequestReceivedEventArgs args) {
            return this.RequestReceived?.Invoke(sender, args);
        }

        /// <summary>
        /// If all the connections are removed this will be true.
        /// Setting this closes and removes all the connections.
        /// </summary>
        public bool IsClosed {
            get {
                lock (_collection) {
                    return _collection.Count <= 0;
                }
            }
            set {
                lock (_collection) {
                    foreach (var connection in _collection.ToArray()) {
                        connection.IsClosed = true;
                    }
                }
            }
        }

        public event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;
        private void Item_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            lock (_collection) {
                args.Connection.ConnectionClosed -= Item_ConnectionClosed;
                _collection.Remove(args.Connection);

                if (_collection.Count <= 0) {
                    ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs() { Connection = this });
                }
            }
        }

        public void Remove(IConnection connection) {
            lock (_collection) {
                connection.ConnectionClosed -= Item_ConnectionClosed;
                _collection.Remove(connection);

                if (_collection.Count <= 0) {
                    ConnectionClosed?.Invoke(this, new ConnectionClosedEventArgs() { Connection = this });
                }
            }
        }

        public event RequestReceived RequestReceived;

        public bool Equals(IConnection other) {
            return ReferenceEquals(this, other);
        }

        #region Not Implemented By Design
#pragma warning disable RCS1079 // Throwing of new NotImplementedException. By Design

        string IConnection.ConnectionId => throw new NotImplementedException("By Design");

        Task IConnection.Connect(CancellationToken cancellationToken = default(CancellationToken)) {
            throw new NotImplementedException("By Design");
        }

        Task<ResponsePacket> IConnection.Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            throw new NotImplementedException("By Design");
        }

        IConnection IConnection.Clone() {
            throw new NotImplementedException("By Design");
        }

#pragma warning restore RCS1079 // Throwing of new NotImplementedException.
        #endregion Not Implemented By Design
    }
}
