using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        public async Task LoadTest() {
            var port = NextPort;

            const int MaxUserId = 400;

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port)), new TestUserProvider(), serializer, new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                var clients = new Dictionary<string, Client>();

                for (var i = 0; i < MaxUserId; i++) {
                    var name = i.ToString();

                    var client = new LoadTestClient(port, MaxUserId, serializer, new TestAuthenticator(name));
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

                if (perSec < 1000) Assert.Fail($"Per second too slow");
            }
        }

        private static IEnumerable<int> GetNextRandomNumber(int min, int max) {
            while (true) {
                yield return _random.Next(min, max);
            }
        }

        private static IEnumerable<Task> GenerateRequestTasks(Client client, int maxUserId) {
            while (true) {
                string toUser = GetNextRandomNumber(0, maxUserId)
                                .Select(id => id.ToString())
                                .First(id => id != client.User.Id);

                var request = new Message() {
                    Destination = toUser,
                    Body = $"Hi from {client.User.Id} to {toUser}"
                };

                yield return client.Request(request);
            }
        }

        public class LoadTestClient : TestClient
        {
            private readonly int _maxUserId;
            public LoadTestClient(int port, int maxUserId, ISerializer serializer, IAuthenticator authenticator) : base(port, serializer, authenticator) {
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
