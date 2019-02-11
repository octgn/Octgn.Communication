using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class LoadTests : TestBase
    {
        private static readonly Random _random = new Random();

        [TestCase]
        [Category("LoadTest")]
        public async Task LoadTest() {
            var port = NextPort;

            const int MaxUserId = 100;
            const int TotalMessageCount = 5000;

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new TestHandshaker()), serializer)) {
                server.Initialize();

                var clients = new Dictionary<string, Client>();

                for (var i = 0; i < MaxUserId; i++) {
                    var name = i.ToString();

                    var client = new LoadTestClient(MaxUserId, CreateConnectionCreator(name), serializer);
                    await client.Connect("localhost");

                    clients.Add(name, client);
                }

                Console.WriteLine("All clients connected");

                var tasks = GenerateRequestTasks(clients, MaxUserId, TotalMessageCount);

                var sw = new Stopwatch();

                sw.Start();

                try {
                    await Task.WhenAll(tasks);
                } finally {

                    sw.Stop();

                    foreach (var client in clients.Values) {
                        client.Dispose();

                        while (client.Status != ConnectionStatus.Disconnected) {
                            Thread.Yield();
                        }
                    }

                    Console.WriteLine(server.PacketCount + " - " + sw.Elapsed);

                    var perSec = server.PacketCount / sw.Elapsed.TotalSeconds;
                    Console.WriteLine($"Total     : {server.PacketCount}");
                    Console.WriteLine($"Per Second: {perSec}");

                    if (perSec < 3000) Assert.Fail($"FAILED: Per second {perSec} too slow");
                }

            }
        }

        private static IEnumerable<int> GetNextRandomNumber(int min, int max) {
            while (true) {
                yield return _random.Next(min, max);
            }
        }

        private static IEnumerable<Task> GenerateRequestTasks(Dictionary<string, Client> clients, int maxUserId, int totalMessageCount) {
            for (var i = 0; i < totalMessageCount; i++) {
                var fromId = GetNextRandomNumber(0, maxUserId).First().ToString();

                var toUser = GetNextRandomNumber(0, maxUserId)
                                .Select(id => id.ToString())
                                .First(id => id != fromId);

                var fromClient = clients[fromId];

                var request = new Message() {
                    Destination = toUser,
                    Body = $"Hi from {fromClient.User.Id} to {toUser}"
                };

                yield return RequestTask(fromClient, request);
            }
        }

        private static async Task RequestTask(Client client, RequestPacket request) {
            var now = DateTime.Now;

            var result = await client.Request(request);
        }

        public class LoadTestClient : Client
        {
            private readonly int _maxUserId;
            public LoadTestClient(int maxUserId, IConnectionCreator clientConnectionProvider, ISerializer serializer)
                : base(clientConnectionProvider, serializer) {
                _maxUserId = maxUserId;
            }

            protected override Task OnRequestReceived(object sender, RequestReceivedEventArgs args) {
                var message = (Message)args.Request;

                Assert.NotNull(message);

                args.Response = new ResponsePacket(args.Request);
                args.IsHandled = true;

                return Task.CompletedTask;
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                }
                base.Dispose(disposing);
            }
        }
    }
}
