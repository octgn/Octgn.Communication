using System;
using System.Net;

namespace Octgn.Communication.Test
{
    public class TestClient : Client
    {
        public int Port { get; set; }
        private readonly ISerializer _serializer;
        private readonly IHandshaker _handshaker;
        public TestClient(int port, ISerializer serializer, IHandshaker handshaker) : base() {
            Port = port;
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
            _handshaker = handshaker;
        }

        protected override IConnection CreateConnection() {
            return new TcpConnection(GetEndpoint().ToString(), _serializer, _handshaker);
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, Port);
        }
    }
}
