using System;
using NUnit.Framework;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Octgn.Communication.Serializers;
using Octgn.Communication.Packets;
using FakeItEasy;

namespace Octgn.Communication.Chat.Test
{
    [TestFixture]
    [Parallelizable(ParallelScope.None)]
    public class Implementation
    {
        private const int MaxTimeout = 10000;

        [SetUp]
        public void Setup() {
            ConnectionBase.WaitForResponseTimeout = TimeSpan.FromSeconds(10);
#if (DEBUG)
            while (NullLogger.LogMessages.Count > 0) {
                if (NullLogger.LogMessages.TryDequeue(out var result)) {
                }
            }
#endif
        }

        [TearDown]
        public void TearDown() {
            GC.Collect();
            GC.WaitForPendingFinalizers();

            Console.WriteLine("===== EXCEPTIONS ======");
            while(Signal.Exceptions.Count > 0) {
                if(Signal.Exceptions.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }
#if (DEBUG)
            Console.WriteLine("===== LOGS ======");
            while (NullLogger.LogMessages.Count > 0) {
                if (NullLogger.LogMessages.TryDequeue(out var result)) {
                    Console.WriteLine(result.ToString());
                }
            }
#endif
            Assert.Zero(Signal.Exceptions.Count, "Unhandled exceptions found in Signal");
        }

        [TestCase]
        public async Task FailedAuthenticationCausesServerToDisconnectClient() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7902);

            var authenticationHandler = A.Fake<IAuthenticationHandler>();

            A.CallTo(() => authenticationHandler.Authenticate(A<Server>.Ignored, A<IConnection>.Ignored, A<AuthenticationRequestPacket>.Ignored))
                .Returns(Task.FromResult(new AuthenticationResult() {
                    ErrorCode = "TestError"
                }));

            using (var server = new Server(new TcpListener(endpoint), new TestConnectionProvider(), new XmlSerializer(), authenticationHandler)) {
                var serverModule = new TestServerModule();
                server.Attach(serverModule);
                server.IsEnabled = true;

                // Need to handle the 'hello' packet we send
                serverModule.Request += (sender, args) => {
                    if (args.Packet.Name == "hello")
                        args.IsHandled = true;
                };

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("bad"))) {
                    try {
                        await client.Connect();
                    } catch (AuthenticationException ex) {
                        Assert.AreEqual(ex.ErrorCode, "TestError");
                    }

                    try {
                        await client.Request(new RequestPacket("hello"));
                        Assert.Fail("client request succeeded, it shouldn't have");
                    } catch (DisconnectedException) { }
                    catch (NotConnectedException) { }


                    // make sure we're not connected
                    Assert.IsFalse(client.IsConnected);
                }
            }
        }

        [TestCase]
        public async Task SendUserMessage() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7903);

            using (var server = new Server(new TcpListener(endpoint), new TestConnectionProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeChat();
                    clientB.InitializeChat();

                    await clientA.Connect();
                    await clientB.Connect();

                    using (var eveMessageReceived = new AutoResetEvent(false)) {

                        string messageBody = null;

                        clientB.Chat().MessageReceived += (sender, args) => {
                            messageBody = args.Message.Body;
                            eveMessageReceived.Set();
                        };

                        var result = await clientA.SendMessage(clientB.UserId, "asdf");

                        Assert.IsNotNull(result);

                        Assert.AreEqual("asdf", messageBody);

                        if (!eveMessageReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientB never got their message :(");
                    }
                }
            }
        }

        [TestCase]
        public async Task ReceiveUserSubscriptionUpdates() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7907);

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(endpoint), new TestConnectionProvider(), serializer, new TestAuthenticationHandler())) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.ReconnectRetryDelay = TimeSpan.FromSeconds(1);
                    clientB.ReconnectRetryDelay = TimeSpan.FromSeconds(1);


                    clientA.InitializeChat();
                    clientB.InitializeChat();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {

                        UserSubscription update = null;

                        var updateCount = 0;

                        clientA.Chat().UserSubscriptionUpdated += (sender, args) => {
                            update = args.Subscription;
                            updateCount++;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect();

                        var result = await clientA.Chat().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(update);

                        Assert.AreEqual(UpdateType.Add, update.UpdateType);
                        Assert.AreEqual("clientA", update.SubscriberUserId);
                        Assert.AreEqual("clientB", update.UserId);
                        Assert.AreEqual(null, update.Category);

                        update.Category = "chicken";

                        result = null;
                        result = await clientA.Chat().RPC.UpdateUserSubscription(update);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(result);

                        Assert.AreEqual(UpdateType.Update, update.UpdateType);
                        Assert.AreEqual("clientA", update.SubscriberUserId);
                        Assert.AreEqual("clientB", update.UserId);
                        Assert.AreEqual("chicken", update.Category);

                        result = null;
                        await clientA.Chat().RPC.RemoveUserSubscription(update.Id);

                        Assert.AreEqual(UpdateType.Remove, update.UpdateType);
                        Assert.AreEqual("clientA", update.SubscriberUserId);
                        Assert.AreEqual("clientB", update.UserId);
                        Assert.AreEqual("chicken", update.Category);

                        Assert.AreEqual(3, updateCount);
                    }
                }
            }
        }

        [TestCase]
        public async Task ReceiveUserUpdates() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7908);

            TestConnectionProvider userProvider = null;

            using (var server = new Server(new TcpListener(endpoint), userProvider = new TestConnectionProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeChat();
                    clientB.InitializeChat();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {

                        UserUpdatedEventArgs updatedUserArgs = null;

                        clientA.Chat().UserUpdated += (sender, args) => {
                            updatedUserArgs = args;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect();

                        var result = await clientA.Chat().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.UserId);
                        Assert.AreEqual(TestConnectionProvider.OfflineStatus, updatedUserArgs.UserStatus);

                        updatedUserArgs = null;

                        await clientB.Connect();

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.UserId);
                        Assert.AreEqual(TestConnectionProvider.OnlineStatus, updatedUserArgs.UserStatus);

                        clientB.Connection.IsClosed = true;

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.UserId);
                        Assert.AreEqual(TestConnectionProvider.OfflineStatus, updatedUserArgs.UserStatus);
                    }
                }
            }
        }


        [TestCase]
        public async Task SendMessageToOfflineUser_ThrowsException() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7906);

            using (var server = new Server(new TcpListener(endpoint), new TestConnectionProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA"))) {
                    await clientA.Connect();

                    try {
                        var result = await clientA.Request(new Message("clientB", "asdf"));
                        Assert.Fail("Request should have failed");
                    } catch (ErrorResponseException ex) {
                        Assert.AreEqual(Octgn.Communication.ErrorResponseCodes.UserOffline, ex.Code);
                    }
                }
            }
        }
    }

    public class TestConnectionProvider : IConnectionProvider
    {
        public const string OnlineStatus = nameof(OnlineStatus);
        public const string OfflineStatus = nameof(OfflineStatus);

        private UserConnectionMap OnlineUsers { get; set; }

        private Server _server;

        public TestConnectionProvider() {
            OnlineUsers = new UserConnectionMap();
            OnlineUsers.UserConnectionChanged += OnlineUsers_UserConnectionChanged;
        }

        private async void OnlineUsers_UserConnectionChanged(object sender, UserConnectionChangedEventArgs e) {
            try {
                await _server.UpdateUserStatus(e.UserId, e.IsConnected ? TestConnectionProvider.OnlineStatus : TestConnectionProvider.OfflineStatus);
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        public IEnumerable<IConnection> GetConnections(string userId) {
            return OnlineUsers.GetConnections(userId);
        }

        public void Initialize(Server server) {
            _server = server;
        }

        public string GetUserId(IConnection connection) {
            return OnlineUsers.GetUserId(connection);
        }

        public Task AddConnection(IConnection connection, string userId) {
            return OnlineUsers.AddConnection(connection, userId);
        }

        public string GetUserStatus(string userId) {
            return OnlineUsers.GetConnections(userId).Any() ? OnlineStatus : OfflineStatus;
        }
    }

    public class TestChatDataProvider : IDataProvider
    {
        public Dictionary<string, IList<UserSubscription>> Subscriptions { get; set; }
        public string GameServerName { get; set; } = "gameserv-unittest";

        public int IndexCounter = 0;

        public event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;

        public TestChatDataProvider() {
            Subscriptions = new Dictionary<string, IList<UserSubscription>>();
        }

        public virtual void AddUserSubscription(UserSubscription subscription) {
            subscription.Id = (++IndexCounter).ToString();

            if(!Subscriptions.TryGetValue(subscription.SubscriberUserId, out IList<UserSubscription> subscriptions)) {
                Subscriptions.Add(subscription.SubscriberUserId, subscriptions = new List<UserSubscription>());
            }
            subscriptions.Add(subscription);

            subscription.UpdateType = UpdateType.Add;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscription
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public virtual IEnumerable<string> GetUserSubscribers(string user) {
            return Subscriptions.Where(usub => usub.Value.Any(sub => sub.UserId == user)).Select(usub => usub.Key);
        }

        public virtual IEnumerable<UserSubscription> GetUserSubscriptions(string user) {
            if(!Subscriptions.TryGetValue(user, out IList<UserSubscription> subscriptions)) {
                Subscriptions.Add(user, subscriptions = new List<UserSubscription>());
            }
            return subscriptions;
        }

        public virtual void RemoveUserSubscription(string subscriptionId) {
            var sub = this.GetUserSubscription(subscriptionId);

            var subscriptions = Subscriptions[sub.SubscriberUserId];
            var subscriptionToRemove = subscriptions.First(x => x.Id == subscriptionId);
            subscriptions.Remove(subscriptionToRemove);

            subscriptionToRemove.UpdateType = UpdateType.Remove;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscriptionToRemove
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public virtual void UpdateUserSubscription(UserSubscription subscription) {
            var subscriptions = Subscriptions[subscription.SubscriberUserId];
            var subscriptionToUpdate = subscriptions.First(x => x.Id == subscription.Id);
            var index = subscriptions.IndexOf(subscriptionToUpdate);

            subscriptions[index] = subscription;

            subscription.UpdateType = UpdateType.Update;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscription
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public UserSubscription GetUserSubscription(string subscriptionId) {
            return Subscriptions.SelectMany(userSubs => userSubs.Value).FirstOrDefault(sub => sub.Id == subscriptionId);
        }
    }

    public class TestAuthenticator : IAuthenticator
    {
        public string UserId { get; set; }

        public TestAuthenticator(string userId) {
            UserId = userId;
        }

        public async Task<AuthenticationResult> Authenticate(Client client, IConnection connection) {
            var authRequest = new AuthenticationRequestPacket("asdf") {
                ["userid"] = UserId
            };
            var result = await client.Request(authRequest);
            return result.As<AuthenticationResult>();
        }
    }

    public class TestAuthenticationHandler : IAuthenticationHandler
    {
        public Task<AuthenticationResult> Authenticate(Server server, IConnection connection, AuthenticationRequestPacket packet) {
            var userId = (string)packet["userid"];

            return Task.FromResult(AuthenticationResult.Success(userId));
        }
    }

    public class TestServerModule : IServerModule
    {
        public event EventHandler<HandleRequestEventArgs> Request;

        public Task HandleRequest(object sender, HandleRequestEventArgs args) {
            Request?.Invoke(sender, args);
            return Task.CompletedTask;
        }

        public Task UserStatucChanged(object sender, UserStatusChangedEventArgs e) {
            return Task.CompletedTask;
        }
    }
}
