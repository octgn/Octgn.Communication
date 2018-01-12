using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class ClientTests : TestBase
    {
        [TestCase]
        public async Task ConnectSucceeds_ConnectedEvent_ThrowsException()
        {
            var port = NextPort;

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port)), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                var expectedException = new NotImplementedException();

                void Signal_OnException(object sender, ExceptionEventArgs args) {
                    if (args.Exception != expectedException)
                        throw args.Exception;
                }

                try {
                    Signal.OnException += Signal_OnException;
                    using (var client = new TestClient(port, new XmlSerializer(), new TestAuthenticator("a"))) {
                        client.Connected += (_, __) => throw expectedException;
                        await client.Connect();
                    }
                } finally {
                    Signal.OnException -= Signal_OnException;
                }
            }
        }

        [TestCase]
        public async Task Connect_ThrowsException_ConnectCalledMoreThanOnce()
        {
            var port = NextPort;

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port)), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                using (var client = new TestClient(port, new XmlSerializer(), new TestAuthenticator("a"))) {
                    await client.Connect();

                    try
                    {
                        await client.Connect();
                        Assert.Fail("Should have thrown an exception");
                    } catch (InvalidOperationException)
                    {
                        Assert.Pass();
                    }
                }
            }
        }

        [TestCase]
        public async Task RequestReceived_GetsInvoked_WhenRequestIsReceived()
        {
            var port = NextPort;

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port)), new TestUserProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                using (var client = new TestClient(port, new XmlSerializer(), new TestAuthenticator("userA"))) {
                    await client.Connect();

                    var tcs = new TaskCompletionSource<RequestPacket>();

                    client.RequestReceived += (_, args) => {
                        args.Response = new ResponsePacket(args.Request);
                        tcs.SetResult(args.Request);
                        return Task.CompletedTask;
                    };

                    var request = new RequestPacket("test");
                    await server.ConnectionProvider.GetConnections("userA").First().Request(request);

                    var delayTask = Task.Delay(10000);

                    var completedTask = await Task.WhenAny(tcs.Task, delayTask);

                    if (completedTask == delayTask) throw new TimeoutException();

                    Assert.NotNull(tcs.Task.Result);
                }
            }
        }
    }

    public class TestAuthenticator : IAuthenticator
    {
        public string UserId { get; set; }

        public TestAuthenticator(string userId) {
            UserId = userId;
        }

        public async Task<AuthenticationResult> Authenticate(Client client, IConnection connection, CancellationToken cancellationToken = default(CancellationToken)) {
            var authRequest = new AuthenticationRequestPacket("asdf") {
                ["userid"] = UserId
            };
            var result = await client.Request(authRequest, cancellationToken);
            return result.As<AuthenticationResult>();
        }
    }

    public class TestAuthenticationHandler : IAuthenticationHandler
    {
        public Task<AuthenticationResult> Authenticate(Server server, IConnection connection, AuthenticationRequestPacket packet, CancellationToken cancellationToken = default(CancellationToken)) {
            var userId = (string)packet["userid"];

            return Task.FromResult(AuthenticationResult.Success(new User(userId, userId)));
        }
    }
}
