using System;
using NUnit.Framework;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Messages;
using System.Collections.Generic;
using System.Linq;
using Octgn.Communication.Serializers;

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
        public async Task TryingToSendMessagesWhenNotLoggedInReturnsPacketWithUnauthorizedAccessException() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7902);

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.IsEnabled = true;

                using (var client = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    var loginResult = await client.Connect($"#{LoginResultType.EmailUnverified}", "`bad");

                    Assert.AreEqual(LoginResultType.EmailUnverified, loginResult);

                    // make sure we're still connected
                    Assert.IsFalse(client.Connection.IsClosed);

                    try {
                        var result = await client.Request(new Message("asdf", "asdf"));
                        Assert.IsNull(result);
                        Assert.Fail("Should have thrown an UnauthroizedAccessException");
                    } catch (ErrorResponseException ex) {
                        Assert.AreEqual(Octgn.Communication.ErrorResponseCodes.UnauthorizedRequest, ex.Code);
                    }
                }
            }
        }

        [TestCase]
        public async Task SendUserMessage() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7903);

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer()))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    clientA.InitializeChat();
                    clientB.InitializeChat();

                    Assert.AreEqual(LoginResultType.Ok, await clientA.Connect($"clientA", ""));
                    Assert.AreEqual(LoginResultType.Ok, await clientB.Connect($"clientB", ""));

                    using (var eveMessageReceived = new AutoResetEvent(false)) {

                        string messageBody = null;

                        clientB.Chat().MessageReceived += (sender, args) => {
                            messageBody = args.Message.Body;
                            eveMessageReceived.Set();
                        };

                        var result = await clientA.SendMessage(clientB.Me, "asdf");

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

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), serializer)) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer()))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
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

                        await clientA.Connect($"clientA", "");

                        var result = await clientA.Chat().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(update);

                        Assert.AreEqual(UpdateType.Add, update.UpdateType);
                        Assert.AreEqual("clientA", update.Subscriber);
                        Assert.AreEqual("clientB", update.User);
                        Assert.AreEqual(null, update.Category);

                        update.Category = "chicken";

                        result = null;
                        result = await clientA.Chat().RPC.UpdateUserSubscription(update);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(result);

                        Assert.AreEqual(UpdateType.Update, update.UpdateType);
                        Assert.AreEqual("clientA", update.Subscriber);
                        Assert.AreEqual("clientB", update.User);
                        Assert.AreEqual("chicken", update.Category);

                        result = null;
                        await clientA.Chat().RPC.RemoveUserSubscription(update.Id);

                        Assert.AreEqual(UpdateType.Remove, update.UpdateType);
                        Assert.AreEqual("clientA", update.Subscriber);
                        Assert.AreEqual("clientB", update.User);
                        Assert.AreEqual("chicken", update.Category);

                        Assert.AreEqual(3, updateCount);
                    }
                }
            }
        }

        [TestCase]
        public async Task ReceiveUserUpdates() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7908);

            TestUserProvider userProvider = null;

            using (var server = new Server(new TcpListener(endpoint), userProvider = new TestUserProvider(), new XmlSerializer())) {
                server.Attach(new ChatServerModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                userProvider.AddUser(new User("clientB") {
                    Status = User.OfflineStatus
                });

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer()))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    clientA.InitializeChat();
                    clientB.InitializeChat();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {

                        User update = null;

                        clientA.Chat().UserUpdated += (sender, args) => {
                            update = args.User;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect($"clientA", "");

                        var result = await clientA.Chat().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(update);

                        Assert.AreEqual(nameof(clientB), update.UserId);
                        Assert.AreEqual(User.OfflineStatus, update.Status);

                        await clientB.Connect($"clientB", "");

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(update);

                        Assert.AreEqual(nameof(clientB), update.UserId);
                        Assert.AreEqual(User.OnlineStatus, update.Status);

                        clientB.Connection.IsClosed = true;

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(update);

                        Assert.AreEqual(nameof(clientB), update.UserId);
                        Assert.AreEqual(User.OfflineStatus, update.Status);
                    }
                }
            }
        }


        [TestCase]
        public async Task SendMessageToOfflineUser_ThrowsException() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7906);

            using (var server = new Server(new TcpListener(endpoint), new TestUserProvider(), new XmlSerializer())) {
                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer())) {
                    Assert.AreEqual(LoginResultType.Ok, await clientA.Connect($"clientA", ""));

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

    public class TestUserProvider : IUserProvider
    {
        public IList<User> Users { get; set; } = new List<User>();
        private UserConnectionMap OnlineUsers { get; set; }

        private Server _server;

        public TestUserProvider() {
            OnlineUsers = new UserConnectionMap();
            OnlineUsers.UserConnectionChanged += OnlineUsers_UserConnectionChanged;
        }

        private async void OnlineUsers_UserConnectionChanged(object sender, UserConnectionChangedEventArgs e) {
            try {
                await _server.UpdateUserStatus(e.User, e.IsConnected ? User.OnlineStatus : User.OfflineStatus);
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        public void AddUser(User user) {
            Users.Add(user);
        }

        public virtual User GetUser(string username) {
            return Users.FirstOrDefault(user => user.UserId == username);
        }

        public virtual void UpdateUser(User user) {
            var dbUser = Users.FirstOrDefault(u => u.UserId == user.UserId);
            dbUser.Status = user.Status;
            throw new NotImplementedException();
        }

        public User ValidateConnection(IConnection connection) {
            return OnlineUsers.ValidateConnection(connection);
        }

        public IEnumerable<IConnection> GetConnections(string username) {
            return OnlineUsers.GetConnections(username);
        }

        public Task AddConnection(IConnection connection, User user) {
            return OnlineUsers.AddConnection(connection, user);
        }

        public virtual LoginResultType ValidateUser(string username, string password, out User user) {
            if (username.StartsWith("#")) {
                user = null;
                return (LoginResultType)Enum.Parse(typeof(LoginResultType), username.Substring(1));
            }

            user = new User(username);
            return LoginResultType.Ok;
        }

        public void Initialize(Server server) {
            _server = server;
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

            if(!Subscriptions.TryGetValue(subscription.Subscriber, out IList<UserSubscription> subscriptions)) {
                Subscriptions.Add(subscription.Subscriber, subscriptions = new List<UserSubscription>());
            }
            subscriptions.Add(subscription);

            subscription.UpdateType = UpdateType.Add;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscription
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public virtual IEnumerable<string> GetUserSubscribers(string user) {
            return Subscriptions.Where(usub => usub.Value.Any(sub => sub.User == user)).Select(usub => usub.Key);
        }

        public virtual IEnumerable<UserSubscription> GetUserSubscriptions(string user) {
            if(!Subscriptions.TryGetValue(user, out IList<UserSubscription> subscriptions)) {
                Subscriptions.Add(user, subscriptions = new List<UserSubscription>());
            }
            return subscriptions;
        }

        public virtual void RemoveUserSubscription(string subscriptionId) {
            var sub = this.GetUserSubscription(subscriptionId);

            var subscriptions = Subscriptions[sub.Subscriber];
            var subscriptionToRemove = subscriptions.First(x => x.Id == subscriptionId);
            subscriptions.Remove(subscriptionToRemove);

            subscriptionToRemove.UpdateType = UpdateType.Remove;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscriptionToRemove
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public virtual void UpdateUserSubscription(UserSubscription subscription) {
            var subscriptions = Subscriptions[subscription.Subscriber];
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
}
