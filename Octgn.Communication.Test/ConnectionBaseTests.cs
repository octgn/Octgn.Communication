using FakeItEasy;
using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class ConnectionBaseTests : TestBase {
        [TestCase]
        public void Equals() {
            var serializer = new XmlSerializer();
            using (var clientA = A.Fake<Client>())
            using (var clientB = A.Fake<Client>())
            using (var conA = new InMemoryConnection(new FakeHandshaker("conA"), serializer, clientA))
            using (var conB = new InMemoryConnection(new FakeHandshaker("conB"), serializer, clientB)) {
                Assert.False(conA.Equals(conB));
                Assert.True(conA.Equals(conA));
            }
        }

        [TestCase]
        public void Initialize_SetsClient() {
            var serializer = new XmlSerializer();
            using(var client = A.Fake<Client>())
            using (var conA = new InMemoryConnection(new FakeHandshaker("conA"), serializer, client)) {
                Assert.AreEqual(conA.Client, client);
            }
        }

        [TestCase]
        public async Task InMemoryTest() {
            var serializer = new XmlSerializer();
            using (var clientA = A.Fake<Client>())
            using (var clientB = A.Fake<Client>())
            using (var conA = new InMemoryConnection(new FakeHandshaker("conA"), serializer, clientA))
            using (var conB = new InMemoryConnection(new FakeHandshaker("conB"), serializer, clientB)) {
                conA.Attach(conB);

                var conACounter = 0;
                var conBCounter = 0;

                conA.RequestReceived += (_, args) => {
                    conACounter++;
                    args.IsHandled = true;
                    return Task.FromResult<object>(null);
                };

                conB.RequestReceived += (_, args) => {
                    conBCounter++;
                    args.IsHandled = true;
                    return Task.FromResult<object>(null);
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

    public class InMemoryConnection : ConnectionBase {
        public InMemoryConnection(IHandshaker handshaker, ISerializer serializer, Client client) : base("inmemory", handshaker, serializer, client) {
        }

        private InMemoryConnection _attachedConnection;

        public void Attach(InMemoryConnection connection) {
            if (_attachedConnection != null || connection._attachedConnection != null)
                throw new InvalidOperationException($"Connection already attached.");

            _attachedConnection = connection;
            connection._attachedConnection = this;
        }

        protected override Task ConnectImpl(CancellationToken cancellationToken = default(CancellationToken)) {
            if (_attachedConnection == null)
                throw new InvalidOperationException("Nothing to connect to.");

            return Task.CompletedTask;
        }

        protected override Task SendImpl(ulong packetId, byte[] data, CancellationToken cancellationToken) {
            return _attachedConnection.ProcessReceivedData(packetId, data, Serializer);
        }

        public override IConnection Clone() {
            throw new NotImplementedException("By Design");
        }
    }

    public class FakeHandshaker : IHandshaker
    {
        private readonly User _user;
        public FakeHandshaker(string userId) {
            _user = new User(userId, userId);
        }

        public Task<HandshakeResult> Handshake(IConnection connection, CancellationToken cancellation) {
            return Task.FromResult(new HandshakeResult() {
                Successful = true,
                User = _user
            });
        }

        public Task<HandshakeResult> OnHandshakeRequest(HandshakeRequestPacket request, IConnection connection, CancellationToken cancellation) {
            throw new NotImplementedException();
        }
    }
}
