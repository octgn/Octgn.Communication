using NUnit.Framework;
using Octgn.Communication.Serializers;
using Octgn.Communication.Tcp;
using System;
using System.Net;
using System.Threading.Tasks;

namespace Octgn.Communication.Test.WindowsDesktop
{
    [TestFixture]
    public class TcpConnectionTests : TestBase
    {
        [Test]
        public async Task ReadOverflow() {
            var port = NextPort;

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);

            var handshaker = new FakeHandshaker("0");

            var serializer = new XmlSerializer();

            var connectionCreated = new TaskCompletionSource<IConnection>();

            using (var server = new Server(new TcpListener(endpoint, handshaker), serializer)) {
                server.Listener.ConnectionCreated += (sender, args) => {
                    connectionCreated.SetResult(args.Connection);
                };

                server.Initialize();

                var connectionCreator = new TcpConnectionCreator(handshaker);

                var client = new Client(connectionCreator, serializer);

                await client.Connect($"localhost:{port}");

                var serverConnection = await connectionCreated.Task;

                var tcpConnection = (TcpConnection)client.Connection;

                var tcpClient = tcpConnection.TcpClient;

                var clientStream = tcpClient.GetStream();

                clientStream.Write(new byte[8], 0, 8);

                var len = BitConverter.GetBytes(-10);

                clientStream.Write(len, 0, len.Length);

                await Task.Delay(500);
            }

            Assert.AreEqual(0, SignalErrors.Count);
        }
    }
}
