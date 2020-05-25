using System;

namespace Octgn.Communication.Tcp
{
    public class TcpConnectionCreator : IConnectionCreator
    {
        public IHandshaker Handshaker { get; }

        public TcpConnectionCreator(IHandshaker handshaker) {
            Handshaker = handshaker ?? throw new ArgumentNullException(nameof(handshaker));
        }

        private Client _client;
        public void Initialize(Client client) {
            _client = client ?? throw new ArgumentNullException(nameof(client));
        }

        public IConnection Create(string host) {
            return new TcpConnection(host, _client.Serializer, Handshaker, _client);
        }
    }
}
