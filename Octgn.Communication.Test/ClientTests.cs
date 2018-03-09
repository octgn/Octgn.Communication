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

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider())) {
                server.Initialize();

                var expectedException = new NotImplementedException();

                void Signal_OnException(object sender, ExceptionEventArgs args) {
                    if (args.Exception != expectedException)
                        throw args.Exception;
                }

                try {
                    Signal.OnException += Signal_OnException;
                    using (var client = new TestClient(port, new XmlSerializer(), new TestHandshaker("a"))) {
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

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider())) {
                server.Initialize();

                using (var client = new TestClient(port, new XmlSerializer(), new TestHandshaker("a"))) {
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

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider())) {
                server.Initialize();

                using (var client = new TestClient(port, new XmlSerializer(), new TestHandshaker("userA"))) {
                    await client.Connect();

                    var tcs = new TaskCompletionSource<RequestPacket>();

                    client.RequestReceived += (_, args) => {
                        args.Response = new ResponsePacket(args.Request);
                        tcs.SetResult(args.Request);
                        return Task.FromResult(args.Response);
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
}
