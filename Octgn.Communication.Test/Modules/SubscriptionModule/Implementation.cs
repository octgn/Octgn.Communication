using System;
using NUnit.Framework;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using Octgn.Communication.Serializers;
using FakeItEasy;
using Octgn.Communication.Modules.SubscriptionModule;

namespace Octgn.Communication.Test.Modules.SubscriptionModule
{
    [TestFixture]
    [NonParallelizable]
    [Parallelizable(ParallelScope.None)]
    public class Implementation : TestBase
    {
        [TestCase]
        public async Task ReceiveUserSubscriptionUpdates() {
            var port = NextPort;

            var serializer = new XmlSerializer();

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider(), new XmlSerializer())) {
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider(server.ConnectionProvider)));

                server.Initialize();

                using (var clientA = CreateClient(port, "clientA"))
                using (var clientB = CreateClient(port, "clientB")) {
                    clientA.ReconnectRetryDelay = TimeSpan.FromSeconds(1);
                    clientB.ReconnectRetryDelay = TimeSpan.FromSeconds(1);

                    clientA.InitializeSubscriptionModule();
                    clientB.InitializeSubscriptionModule();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {
                        UserSubscription update = null;

                        var updateCount = 0;

                        clientA.Subscription().UserSubscriptionUpdated += (sender, args) => {
                            update = args.Subscription;
                            updateCount++;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect("localhost");

                        var result = await clientA.Subscription().RPC.AddUserSubscription("clientB", null);

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
                        result = await clientA.Subscription().RPC.UpdateUserSubscription(update);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(result);

                        Assert.AreEqual(UpdateType.Update, update.UpdateType);
                        Assert.AreEqual("clientA", update.SubscriberUserId);
                        Assert.AreEqual("clientB", update.UserId);
                        Assert.AreEqual("chicken", update.Category);

                        result = null;
                        await clientA.Subscription().RPC.RemoveUserSubscription(update.Id);

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
            var port = NextPort;

            using (var server = new Server(new TcpListener(new IPEndPoint(IPAddress.Loopback, port), new XmlSerializer(), new TestHandshaker()), new InMemoryConnectionProvider(), new XmlSerializer())) {
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider(server.ConnectionProvider)));

                server.Initialize();

                using (var clientA = CreateClient(port, "clientA"))
                using (var clientB = CreateClient(port, "clientB")) {
                    clientA.InitializeSubscriptionModule();
                    clientB.InitializeSubscriptionModule();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {
                        UserUpdatedEventArgs updatedUserArgs = null;

                        clientA.Subscription().UserUpdated += (sender, args) => {
                            updatedUserArgs = args;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect("localhost");

                        var result = await clientA.Subscription().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
                        Assert.AreEqual(TestChatDataProvider.UserOfflineStatus, updatedUserArgs.UserStatus);

                        updatedUserArgs = null;

                        await clientB.Connect("localhost");

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
                        Assert.AreEqual(TestChatDataProvider.UserOnlineStatus, updatedUserArgs.UserStatus);

                        clientB.Connection.Close();

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
                        Assert.AreEqual(TestChatDataProvider.UserOfflineStatus, updatedUserArgs.UserStatus);
                    }
                }
            }
        }
    }

    public class TestChatDataProvider : IDataProvider
    {
        public Dictionary<string, IList<UserSubscription>> Subscriptions { get; set; }
        public string GameServerName { get; set; } = "gameserv-unittest";

        public int IndexCounter = 0;

        public event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;

        private readonly IConnectionProvider _connectionProvider;

        public TestChatDataProvider(IConnectionProvider connectionProvider) {
            Subscriptions = new Dictionary<string, IList<UserSubscription>>();
            _connectionProvider = connectionProvider ?? throw new ArgumentNullException(nameof(connectionProvider));
        }

        public virtual void AddUserSubscription(UserSubscription subscription) {
            subscription.Id = (++IndexCounter).ToString();

            if (!Subscriptions.TryGetValue(subscription.SubscriberUserId, out var subscriptions)) {
                Subscriptions.Add(subscription.SubscriberUserId, subscriptions = new List<UserSubscription>());
            }
            subscriptions.Add(subscription);

            subscription.UpdateType = UpdateType.Add;
            var args = new UserSubscriptionUpdatedEventArgs {
                Subscription = subscription
            };

            UserSubscriptionUpdated?.Invoke(this, args);
        }

        public virtual IEnumerable<string> GetUserSubscribers(string userId) {
            return Subscriptions.Where(usub => usub.Value.Any(sub => sub.UserId == userId)).Select(usub => usub.Key);
        }

        public virtual IEnumerable<UserSubscription> GetUserSubscriptions(string userId) {
            if (!Subscriptions.TryGetValue(userId, out var subscriptions)) {
                Subscriptions.Add(userId, subscriptions = new List<UserSubscription>());
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

        public void SetUserStatus(string userId, string status) {
            throw new NotImplementedException();
        }

        public const string UserOnlineStatus = "Online";
        public const string UserOfflineStatus = "Offline";

        public string GetUserStatus(string userId) {
            return (_connectionProvider.GetConnections(userId).Any(con => con.State == ConnectionState.Connected))
                ? UserOnlineStatus
                : UserOfflineStatus;

            throw new NotImplementedException();
        }
    }
}
