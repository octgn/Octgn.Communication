using Octgn.Communication;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace Load
{
    class Program
    {
        static async Task Main(string[] args) {
            try {
                int maxThreads, complete;

                ThreadPool.GetMaxThreads(out maxThreads, out complete);

                ThreadPool.SetMinThreads(100, complete);

                var program = new Program();

                await program.Run();
            } catch (Exception ex) {
                Console.Error.WriteLine("Unhandled Error: " + ex);
            }

            Console.WriteLine();
            Console.Beep();
            Console.Beep();
            Console.Beep();
            Console.Beep();
            Console.WriteLine("=========================");
            Console.WriteLine("Done");
            Console.WriteLine("=========================");
            Console.ReadKey();
        }

        const int Port = 13412;

        async Task Run() {
            Signal.OnException += (a, b) => {
                Console.Error.WriteLine("Signal: " + b.Message + " " + b.Exception);
            };

            LoggerFactory.DefaultMethod = (c) => new ConsoleLogger(c);
            var port = Port;

            const int MaxUserId = 300;
            const int TotalMessageCount = 20000;

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new TestHandshaker()), new InMemoryConnectionProvider(), serializer)) {
                server.Initialize();

                var clients = new Dictionary<string, Client>();

                for (var i = 0; i <= MaxUserId; i++) {
                    var name = i.ToString();

                    var client = new LoadTestClient(MaxUserId, CreateClientConnectionProvider(port, name), serializer);
                    await client.Connect("localhost");

                    clients.Add(name, client);
                }

                Console.Beep();
                Console.WriteLine("All clients connected.");

                var tasks = GenerateRequestTasks(clients, MaxUserId, TotalMessageCount);

                Console.Beep();
                Console.Beep();
                Console.WriteLine("Starting...");

                var sw = new Stopwatch();

                sw.Start();

                try {
                    await Task.WhenAll(tasks);
                } finally {

                    sw.Stop();

                    foreach (var client in clients.Values) {
                        client.Dispose();

                        while(client.Status != ConnectionStatus.Disconnected) {
                            Thread.Yield();
                        }
                    }

                    Console.WriteLine(server.PacketCount + " - " + sw.Elapsed);

                    var perSec = server.PacketCount / sw.Elapsed.TotalSeconds;
                    Console.WriteLine($"Total     : {server.PacketCount}");
                    Console.WriteLine($"Per Second: {perSec}");

                    if (perSec < 3000) Console.Error.WriteLine($"FAILED: Per second {perSec} too slow");
                }

            }
        }

        protected IClientConnectionProvider CreateClientConnectionProvider(int port, string userId) {
            return new TestClientConnectionProvider(port, new XmlSerializer(), new TestHandshaker(userId));
        }

        private static readonly Random _random = new Random();

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
            public LoadTestClient(int maxUserId, IClientConnectionProvider clientConnectionProvider, ISerializer serializer)
                : base(clientConnectionProvider, serializer) {
                _maxUserId = maxUserId;
            }

            protected override Task OnRequestReceived(object sender, RequestReceivedEventArgs args) {
                var message = (Message)args.Request;

                if (message == null) throw new InvalidOperationException($"Message is null");

                args.IsHandled = true;

                return Task.CompletedTask;
            }

            protected override void Dispose(bool disposing) {
                if (disposing) {
                }
                base.Dispose(disposing);
            }
        }

        public class ConsoleLogger : ILogger
        {
            public void Info(string message) {
                //Write(FormatMessage(message));
            }

            public void Warn(string message) {
                Write(FormatMessage(message));
            }

            public void Warn(string message, Exception ex) {
                Write(FormatMessage(message, ex));
            }

            public void Warn(Exception ex) {
                Write(FormatMessage("Exception", ex));
            }

            public void Error(string message) {
                Write(FormatMessage(message));
            }

            public void Error(string message, Exception ex) {
                Write(FormatMessage(message, ex));
            }

            public void Error(Exception ex) {
                Write(FormatMessage("Exception", ex));
            }

            public string Name { get; }

            public ConsoleLogger(LoggerFactory.Context context) {
                Name = context.Name;
            }

            private string FormatMessage(string message, Exception ex = null, [CallerMemberName]string caller = null) {
                var ext = ex == null ? string.Empty : Environment.NewLine + ex.ToString();
                return $"{caller.ToUpper()}: {Name}: {message} {ext}";
            }

            private void Write(string message) {
                Console.WriteLine(message);
            }
        }
    }
}
