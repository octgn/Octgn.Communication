using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class ConnectionBaseTests : TestBase
    {
        [TestCase]
        public async Task InMemoryTest() {
            using (var conA = new InMemoryConnection())
            using (var conB = new InMemoryConnection()) {
                conA.Attach(conB);

                var conACounter = 0;
                var conBCounter = 0;

                conA.RequestReceived += (sender, args) => {
                    conACounter++;
                    return Task.FromResult(new ResponsePacket(args.Request));
                };

                conB.RequestReceived += (sender, args) => {
                    conBCounter++;
                    return Task.FromResult(new ResponsePacket(args.Request));
                };

                await conA.Connect();
                await conB.Connect();

                for (var i = 0; i < 100; i++) {
                    await conA.Request(new RequestPacket("test"));
                    await conA.Request(new RequestPacket("test"));
                    await conA.Request(new RequestPacket("test"));
                    await conA.Request(new RequestPacket("test"));
                    await conA.Request(new RequestPacket("test"));
                    await conB.Request(new RequestPacket("test"));
                    await conB.Request(new RequestPacket("test"));
                    await conB.Request(new RequestPacket("test"));
                    await conB.Request(new RequestPacket("test"));
                    await conB.Request(new RequestPacket("test"));
                }

                Assert.AreEqual(500, conACounter);
                Assert.AreEqual(500, conBCounter);
            }
        }
    }

    public class InMemoryConnection : ConnectionBase
    {
        public override bool IsConnected => _isConnected;

        private bool _isConnected;

        public InMemoryConnection() {
        }

        private InMemoryConnection _attachedConnection;

        public void Attach(InMemoryConnection connection) {
            if (_attachedConnection != null || connection._attachedConnection != null)
                throw new InvalidOperationException($"Connection already attached.");

            _attachedConnection = connection;
            connection._attachedConnection = this;
        }

        public override Task Connect() {
            if(_attachedConnection == null)
                throw new InvalidOperationException("Nothing to connect to.");

            _isConnected = true;

            return base.Connect();
        }

        public override void Dispose() {
            _isConnected = false;
            _backgroundTasks.Dispose();
            base.Dispose();
        }

        protected ConcurrentQueue<Packet> PacketsIn { get; set; } = new ConcurrentQueue<Packet>();
        private readonly ConcurrentQueue<TaskCompletionSource<Packet>> _signalPacketReceived = new ConcurrentQueue<TaskCompletionSource<Packet>>(new[] {
            new TaskCompletionSource<Packet>(),
            new TaskCompletionSource<Packet>(),
            new TaskCompletionSource<Packet>(),
            new TaskCompletionSource<Packet>(),
            new TaskCompletionSource<Packet>(),
        });

        private readonly BackgroundTasks _backgroundTasks = new BackgroundTasks();

        protected override async Task ReadPacketsAsync() {
            while (_isConnected) {
                var tasks = new List<Task>(_signalPacketReceived.Select(x => (Task)x.Task)) {
                    ClosedCancellationTask
                };

                var task = await Task.WhenAny(tasks);

                if (task == ClosedCancellationTask) break;

                var packetTask = task as Task<Packet>;

                _backgroundTasks.Schedule(ProcessReceivedPacket(packetTask.Result));
            }
        }

        protected override Task SendPacketImplementation(Packet packet) {
            _attachedConnection.AddPacket(packet);
            return Task.CompletedTask;
        }

        protected void AddPacket(Packet packet) {
            _signalPacketReceived.TryDequeue(out TaskCompletionSource<Packet> tcs);
            _signalPacketReceived.Enqueue(new TaskCompletionSource<Packet>());
            tcs.SetResult(packet);
        }

        public override IConnection Clone() {
            throw new NotImplementedException();
        }
    }
}
