using System.Net;

namespace Octgn.Communication.Test
{
    public class TestClient : Client
    {
        public int Port { get; set; }
        public TestClient(int port, ISerializer serializer, IAuthenticator authenticator) : base(serializer, authenticator) {
            Port = port;
        }

        protected override IConnection CreateConnection() {
            return new TcpConnection(GetEndpoint().ToString());
        }

        public IPEndPoint GetEndpoint() {
            return new IPEndPoint(IPAddress.Loopback, Port);
        }
    }
}
