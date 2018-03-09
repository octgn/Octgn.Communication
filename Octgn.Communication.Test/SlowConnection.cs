using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System;
using System.Threading;

namespace Octgn.Communication.Test
{
    public class SlowConnection : IConnection
    {
        private readonly IConnection _connection;

        public string ConnectionId => _connection.ConnectionId;
        public string RemoteAddress => _connection.RemoteAddress;
        public User User => _connection.User;
        public ConnectionState State => _connection.State;
        public IConnection Clone() => new SlowConnection(_connection.Clone());
        public void Close() => _connection.Close();
        public Task Connect(CancellationToken cancellationToken = default(CancellationToken)) => _connection.Connect(cancellationToken);
        public void Dispose() => _connection.Dispose();

        public event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged {
            add { _connection.ConnectionStateChanged += value; }
            remove { _connection.ConnectionStateChanged -= value; }
        }

        public event RequestReceived RequestReceived {
            add { _connection.RequestReceived += value; }
            remove { _connection.RequestReceived -= value; }
        }

        public SlowConnection(IConnection connection) {
            _connection = connection ?? throw new ArgumentNullException(nameof(connection));
        }

        public async Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            await Task.Delay(2000);
            return await _connection.Request(packet, cancellationToken);
        }

        public bool Equals(IConnection other) {
            if (other == null) return false;
            if (!(other is SlowConnection slowConnection)) return false;
            return slowConnection._connection.Equals(_connection);
        }
    }
}
