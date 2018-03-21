using System;
using System.Net;

namespace Octgn.Communication.Test
{
    public class TestClientConnectionProvider : IClientConnectionProvider
    {
        public int Port { get; set; }

        private readonly ISerializer _serializer;
        private readonly IHandshaker _handshaker;
        public TestClientConnectionProvider(int port, ISerializer serializer, IHandshaker handshaker) : base() {
            Port = port;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _handshaker = handshaker;
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, Port);
        }

        public IConnection Create(string host) {
            return new TcpConnection(GetEndpoint().ToString(), _serializer, _handshaker);
            throw new NotImplementedException();
        }
    }
}
