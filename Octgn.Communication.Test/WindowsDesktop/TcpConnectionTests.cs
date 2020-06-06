using NUnit.Framework;
using Octgn.Communication.Serializers;
using Octgn.Communication.Tcp;
using System;
using System.Net;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test.WindowsDesktop
{
    [TestFixture]
    public class TcpConnectionTests : TestBase
    {
        [Test]
        public async Task NegativeLength_ClosesConnection_ButDoesNotSignal() {
            var port = NextPort;

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);

            var handshaker = new FakeHandshaker("0");

            var serializer = new XmlSerializer();

            var connectionCreated = new TaskCompletionSource<IConnection>();

            using (var server = new Server(new TcpListener(endpoint, handshaker), serializer)) {
                server.Listener.ConnectionCreated += (sender, args) => {
                    connectionCreated.SetResult(args.Connection);
                };

                server.Initialize();

                var connectionCreator = new TcpConnectionCreator(handshaker);

                var client = new Client(connectionCreator, serializer);

                await client.Connect($"localhost:{port}");

                var serverConnection = (TcpConnection)await connectionCreated.Task;

                var tcpConnection = (TcpConnection)client.Connection;

                var tcpClient = tcpConnection.TcpClient;

                var clientStream = tcpClient.GetStream();

                clientStream.Write(new byte[8], 0, 8);

                var len = BitConverter.GetBytes(-10);

                clientStream.Write(len, 0, len.Length);

                using (var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100))) {
                    while (!(serverConnection.LastPacketReadingException is InvalidDataLengthException)) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for exception");

                        await Task.Delay(5);
                    }

                    while (tcpClient.Connected) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for client to disconnect");

                        await Task.Delay(5);

                        try {
                            tcpClient.Client.Poll(1, System.Net.Sockets.SelectMode.SelectRead);
                        } catch (ObjectDisposedException) { }
                    }

                    while (serverConnection.State != ConnectionState.Closed) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for server to disconnect");

                        await Task.Delay(5);
                    }
                }
            }

            Assert.AreEqual(0, SignalErrors.Count);
        }

        [Test]
        public async Task TooLongLength_ClosesConnection_ButDoesNotSignal() {
            var port = NextPort;

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);

            var handshaker = new FakeHandshaker("0");

            var serializer = new XmlSerializer();

            var connectionCreated = new TaskCompletionSource<IConnection>();

            using (var server = new Server(new TcpListener(endpoint, handshaker), serializer)) {
                server.Listener.ConnectionCreated += (sender, args) => {
                    connectionCreated.SetResult(args.Connection);
                };

                server.Initialize();

                var connectionCreator = new TcpConnectionCreator(handshaker);

                var client = new Client(connectionCreator, serializer);

                await client.Connect($"localhost:{port}");

                var serverConnection = (TcpConnection)await connectionCreated.Task;

                var tcpConnection = (TcpConnection)client.Connection;

                var tcpClient = tcpConnection.TcpClient;

                var clientStream = tcpClient.GetStream();

                clientStream.Write(new byte[8], 0, 8);

                var len = BitConverter.GetBytes(SerializedPacket.MAX_DATA_SIZE + 1);

                clientStream.Write(len, 0, len.Length);

                using (var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100))) {
                    while (!(serverConnection.LastPacketReadingException is InvalidDataLengthException)) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for exception");

                        await Task.Delay(3);
                    }

                    while (tcpClient.Connected) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for client to disconnect");

                        await Task.Delay(3);

                        try {
                            tcpClient.Client.Poll(1, System.Net.Sockets.SelectMode.SelectRead);
                        } catch (ObjectDisposedException) { }
                    }

                    while (serverConnection.State != ConnectionState.Closed) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for server to disconnect");

                        await Task.Delay(3);
                    }
                }
            }

            Assert.AreEqual(0, SignalErrors.Count);
        }

        [Test]
        public async Task ClientDisconnectBeforeSendingEntirePacket_ClosesConnection_ButDoesNotSignal() {
            var port = NextPort;

            var endpoint = new IPEndPoint(IPAddress.Loopback, port);

            var handshaker = new FakeHandshaker("0");

            var serializer = new XmlSerializer();

            var connectionCreated = new TaskCompletionSource<IConnection>();

            using (var server = new Server(new TcpListener(endpoint, handshaker), serializer)) {
                server.Listener.ConnectionCreated += (sender, args) => {
                    connectionCreated.SetResult(args.Connection);
                };

                server.Initialize();

                var connectionCreator = new TcpConnectionCreator(handshaker);

                var client = new Client(connectionCreator, serializer);

                await client.Connect($"localhost:{port}");

                var serverConnection = (TcpConnection)await connectionCreated.Task;

                var tcpConnection = (TcpConnection)client.Connection;

                var tcpClient = tcpConnection.TcpClient;

                var clientStream = tcpClient.GetStream();

                // Write half of a packet id
                clientStream.Write(new byte[4], 0, 4);

                tcpConnection.Close();

                Assert.IsFalse(client.IsConnected);

                using (var timeoutCancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100))) {
                    while (!(serverConnection.LastPacketReadingException is DisconnectedException)) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for DisconnectedException");

                        await Task.Delay(3);
                    }

                    while (serverConnection.State != ConnectionState.Closed) {
                        if (timeoutCancellation.IsCancellationRequested)
                            Assert.Fail("Timed out waiting for server to disconnect");

                        await Task.Delay(3);
                    }
                }
            }

            Assert.AreEqual(0, SignalErrors.Count);
        }
    }
}
