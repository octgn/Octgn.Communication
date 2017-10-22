using System;
using NUnit.Framework;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Modules;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class Implementation : TestBase
    {
        private const int MaxTimeout = 10000;

        [TestCase]
        public async Task TcpLayerOperates() {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("user"))) {
                    await client.Connect();
                }
            }
        }

        [TestCase]
        public async Task TcpLayerSurvivesDisconnect() {
            var endpoint = GetEndpoint();

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), serializer, new TestAuthenticationHandler())) {
                server.Attach(new PingModule());
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), serializer, new TestAuthenticator("user"))) {
                    await client.Connect();

                    var originalConnection = client.Connection;

                    using (var eveClientConnected = new AutoResetEvent(false)) {
                        client.Connected += (a, b) => {
                            eveClientConnected.Set();
                        };

                        // Force close connection on the server, causing the client to try can call back
                        // When it calls back, the server should auto pick it back up. This should be smooth as butter.
                        server.Connections.IsClosed = true; // closes all the connections

                        var oldTimeout = ConnectionBase.WaitForResponseTimeout;
                        try {
                            ConnectionBase.WaitForResponseTimeout = TimeSpan.FromSeconds(2);
                            await client.Connection.Ping();
                            Assert.Fail("Connection breaking should have caused the client send to fail");
                        } catch (Exception) {

                        } finally {
                            ConnectionBase.WaitForResponseTimeout = oldTimeout;
                        }

                        if (!eveClientConnected.WaitOne(MaxTimeout))
                            Assert.Fail("Client never reconnected");

                        await client.Connection.Ping();
                    }
                }
            }
        }

        [TestCase]
        public async Task ClientAsyncActuallyWaits()
        {
            var endpoint = GetEndpoint();

            using (var server = new Server(new SlowListener(new TcpListener(endpoint)), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new PingModule());
                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    await clientA.Connect();
                    await clientB.Connect();

                    var requestTime = await clientA.Connection.Ping();

                    Assert.AreNotEqual(default(DateTime), requestTime);
                }
            }
        }

        [TestCase]
        public async Task CanSendTwoRequests() {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new PingModule());
                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    await clientA.Connect();
                    await clientB.Connect();

                    await clientA.Connection.Ping();
                    await clientA.Connection.Ping();
                }
            }
        }
    }
}
