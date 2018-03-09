using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Net;
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

            const int MaxUserId = 400;

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider())) {
                server.Initialize();

                var clients = new Dictionary<string, Client>();

                for (var i = 0; i < MaxUserId; i++) {
                    var name = i.ToString();

                    var client = new LoadTestClient(port, MaxUserId, serializer, new TestHandshaker(name));
                    await client.Connect();

                    clients.Add(name, client);
                }

                var sw = new Stopwatch();

                var tasks = clients
                    .Values
                    .SelectMany(client => GenerateRequestTasks(client, MaxUserId).Take(20))
                    .ToArray();

                sw.Start();

                await Task.WhenAll(tasks);

                sw.Stop();

                foreach (var client in clients.Values) {
                    client.Dispose();
                }

                Console.WriteLine(server.RequestCount + " - " + sw.Elapsed);

                var perSec = server.RequestCount / sw.Elapsed.TotalSeconds;
                Console.WriteLine($"Per Second: {perSec}");

                if (perSec < 3000) Assert.Fail($"Per second too slow");
            }
        }

        private static IEnumerable<int> GetNextRandomNumber(int min, int max) {
            while (true) {
                yield return _random.Next(min, max);
            }
        }

        private static IEnumerable<Task> GenerateRequestTasks(Client client, int maxUserId) {
            while (true) {
                var toUser = GetNextRandomNumber(0, maxUserId)
                                .Select(id => id.ToString())
                                .First(id => id != client.User.Id);

                var request = new Message() {
                    Destination = toUser,
                    Body = $"Hi from {client.User.Id} to {toUser}"
                };

                yield return RequestTask(client, request);
            }
        }

        private static async Task RequestTask(Client client, RequestPacket request) {
            var now = DateTime.Now;

            var result = await client.Request(request);

            var requestTime = DateTime.Now - now;

            var percentOfTimeout = requestTime.Ticks / (double)ConnectionBase.WaitForResponseTimeout.Ticks;

            var withinPercent = 100 * (1 - percentOfTimeout);

            if (withinPercent <= 20)
                Console.WriteLine($"REQUEST WITHIN {withinPercent}% OF TIMEOUT");
        }

        public class LoadTestClient : TestClient
        {
            private readonly int _maxUserId;
            public LoadTestClient(int port, int maxUserId, ISerializer serializer, IHandshaker handshaker) : base(port, serializer, handshaker) {
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
