using NUnit.Framework;
using Octgn.Communication.Messages;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class ClientTests : TestBase
    {
        [TestCase]
        public async Task ConnectSucceeds_ConnectedEvent_ThrowsException()
        {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    client.Connected += (_, __) => {
                        throw new NotImplementedException();
                    };
                    var result = await client.Connect("user", "pass");

                    Assert.AreEqual(LoginResultType.Ok, result);
                }
            }
        }

        [TestCase]
        public async Task Connect_ThrowsException_ConnectCalledMoreThanOnce()
        {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    var result = await client.Connect("", null);

                    Assert.AreEqual(LoginResultType.Ok, result);

                    try
                    {
                        await client.Connect("", null);
                        Assert.Fail("Should have thrown an exception");
#pragma warning disable CS0168 // Variable is declared but never used
                    } catch (InvalidOperationException ex)
#pragma warning restore CS0168 // Variable is declared but never used
                    {
                        Assert.Pass();
                    }
                }
            }
        }

        [TestCase]
        public async Task RequestReceived_GetsInvoked_WhenRequestIsReceived()
        {
            var endpoint = GetEndpoint();

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    var result = await client.Connect("userA", null);

                    Assert.AreEqual(LoginResultType.Ok, result);

                    var tcs = new TaskCompletionSource<RequestPacket>();

                    client.RequestReceived += (_, args) => {
                        args.Response = new ResponsePacket(args.Request);
                        tcs.SetResult(args.Request);
                    };

                    var request = new RequestPacket("test");
                    await server.UserProvider.GetConnections("userA").First().Request(request);

                    var delayTask = Task.Delay(10000);

                    var completedTask = await Task.WhenAny(tcs.Task, delayTask);

                    if (completedTask == delayTask) throw new TimeoutException();

                    Assert.NotNull(tcs.Task.Result);
                }
            }
        }
    }
}
