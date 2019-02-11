using System;
using System.Net;

namespace Octgn.Communication
{
    public class TcpConnectionCreator : IConnectionCreator
    {
        private readonly IHandshaker _handshaker;
        public TcpConnectionCreator(IHandshaker handshaker) {
            _handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));
        }

        private Client _client;
        public void Initialize(Client client) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IConnection Create(string host) {
            return new TcpConnection(host, _client.Serializer, _handshaker, _client);
        }
    }
}
