using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Utility;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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

                var requests = new List<Task<ResponsePacket>>();

                for (var i = 0; i < 1000; i++) {
                    requests.Add(conA.Request(new RequestPacket("conA")));
                    requests.Add(conB.Request(new RequestPacket("conB")));
                }

                await Task.WhenAll(requests);

                Assert.AreEqual(0, conA.StillAwaitingAck);
                Assert.AreEqual(0, conB.StillAwaitingAck);

                Assert.AreEqual(1000, conACounter);
                Assert.AreEqual(1000, conBCounter);
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

        public override Task Connect(CancellationToken cancellationToken = default(CancellationToken)) {
            if(_attachedConnection == null)
                throw new InvalidOperationException("Nothing to connect to.");

            _isConnected = true;

            return base.Connect(cancellationToken);
        }

        public override void Dispose() {
            _isConnected = false;
            _processPacketsTask.Wait();
            base.Dispose();
        }

        private readonly List<TaskCompletionSource<Packet>> _signalPacketReceived = new List<TaskCompletionSource<Packet>>(new[] {
            new TaskCompletionSource<Packet>()
        });

        private Task _processPacketsTask = Task.CompletedTask;

        protected override async Task ReadPacketsAsync() {
            while (_isConnected) {
                TaskCompletionSource<Packet> firstTcs = null;
                lock (_signalPacketReceived) {
                    firstTcs = _signalPacketReceived[0];
                }

                var tasks = new List<Task>() {
                    firstTcs.Task,
                    ClosedCancellationTask
                };

                var task = await Task.WhenAny(tasks);

                if (task == ClosedCancellationTask) break;

                lock (_signalPacketReceived) {
                    // Remove the first one that just completed
                    _signalPacketReceived.RemoveAt(0);
                    _signalPacketReceived.Add(new TaskCompletionSource<Packet>());
                }

                var packetTask = task as Task<Packet>;

                _processPacketsTask = _processPacketsTask.ContinueWith(async (prevTask) => {
                    await ProcessReceivedPacket(packetTask.Result, this.ClosedCancellationToken);
                }).Unwrap();
            }
        }

        protected void AddPacket(Packet packet) {
            lock (_signalPacketReceived) {
                foreach(var signal in _signalPacketReceived) {
                    if (signal.TrySetResult(packet)) return;
                }

                var tcs = new TaskCompletionSource<Packet>();
                tcs.SetResult(packet);
                _signalPacketReceived.Add(tcs);
            }
        }

        protected override Task SendPacketImplementation(Packet packet, CancellationToken cancellationToken) {
            _attachedConnection.AddPacket(packet);
            return Task.CompletedTask;
        }

        public override IConnection Clone() {
            throw new NotImplementedException();
        }
    }
}
