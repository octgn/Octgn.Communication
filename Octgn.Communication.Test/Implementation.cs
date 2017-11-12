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
                    client.ReconnectRetryDelay = TimeSpan.FromSeconds(1);
                    await client.Connect();

                    var originalConnection = client.Connection;

                    using (var eveClientConnected = new AutoResetEvent(false)) {
                        client.Connected += (a, b) => eveClientConnected.Set();

                        // Force close connection on the server, causing the client to try can call back
                        // When it calls back, the server should auto pick it back up. This should be smooth as butter.
                        server.Connections.IsClosed = true; // closes all the connections

                        var pingSucceeded = false;

                        try {
                            await client.Connection.Ping();
                            pingSucceeded = true;
                        } catch (Exception){

                        }

                        if(pingSucceeded)
                            Assert.Fail("Connection breaking should have caused the client send to fail");

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

        [TestCase]
        public async Task UnhandledPacketFailsProperly() {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                // Don't attach the ping module, that way the server won't know how to handle the ping request.
                //server.Attach(new PingModule());

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA"))) {
                    await clientA.Connect();

                    var delayTask = Task.Delay(10000);

                    var pingTask = clientA.Connection.Ping();

                    try {
                        var result = await clientA.Connection.Ping();
                    } catch (ErrorResponseException ex) {
                        Assert.AreEqual(ErrorResponseCodes.UnhandledRequest, ex.Code);
                        return;
                    }

                    Assert.Fail("Ping request didn't throw an exception");
                }
            }
        }
    }
}
