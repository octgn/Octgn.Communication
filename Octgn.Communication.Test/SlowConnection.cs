using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System;
using System.Threading;

namespace Octgn.Communication.Test
{
    public class SlowConnection : IConnection
    {
        public ISerializer Serializer {
            get => _connection.Serializer;
            set => _connection.Serializer = value;
        }

        public string ConnectionId => _connection.ConnectionId;

        public bool IsClosed { get => _connection.IsClosed; set => _connection.IsClosed = value; }

        private readonly IConnection _connection;

        public event EventHandler<ConnectionClosedEventArgs> ConnectionClosed {
            add => _connection.ConnectionClosed += value;
            remove => _connection.ConnectionClosed -= value;
        }

        public event RequestReceived RequestReceived {
            add => _connection.RequestReceived += value;
            remove => _connection.RequestReceived -= value;
        }

        public SlowConnection(IConnection connection)
        {
            _connection = connection;
        }

        public async Task Connect(CancellationToken cancellationToken = default(CancellationToken))
        {
            await _connection.Connect(cancellationToken);
        }

        public async Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken))
        {
            await Task.Delay(2000);
            return await _connection.Request(packet, cancellationToken);
        }

        public IConnection Clone()
        {
            return new SlowConnection( _connection.Clone());
        }

        public bool Equals(IConnection other)
        {
            if (other == null) return false;
            if (!(other is SlowConnection slowConnection)) return false;
            return slowConnection._connection.Equals(_connection);
        }
    }
}
