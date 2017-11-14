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
using Octgn.Communication.Modules.SubscriptionModule;

namespace Octgn.Communication.Test.Modules.SubscriptionModule
{
    [TestFixture]
    [NonParallelizable]
    [Parallelizable(ParallelScope.None)]
    public class Implementation : TestBase
    {
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
                    if (args.Request.Name == "hello")
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
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeSubscriptionModule();
                    clientB.InitializeSubscriptionModule();

                    await clientA.Connect();
                    await clientB.Connect();

                    using (var eveMessageReceived = new AutoResetEvent(false)) {
                        string messageBody = null;

                        clientB.RequestReceived += (sender, args) => {
                            if (!(args.Request is Message message)) return Task.CompletedTask;

                            messageBody = message.Body;

                            args.Response = new ResponsePacket(args.Request);

                            eveMessageReceived.Set();

                            return Task.CompletedTask;
                        };

                        var result = await clientA.SendMessage(clientB.User.Id, "asdf");

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
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
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

                        await clientA.Connect();

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
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7908);

            TestConnectionProvider userProvider = null;

            using (var server = new Server(new TcpListener(endpoint), userProvider = new TestConnectionProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeSubscriptionModule();
                    clientB.InitializeSubscriptionModule();

                    using (var eveUpdateReceived = new AutoResetEvent(false)) {
                        UserUpdatedEventArgs updatedUserArgs = null;

                        clientA.Subscription().UserUpdated += (sender, args) => {
                            updatedUserArgs = args;
                            eveUpdateReceived.Set();
                        };

                        await clientA.Connect();

                        var result = await clientA.Subscription().RPC.AddUserSubscription("clientB", null);

                        Assert.NotNull(result);

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
                        Assert.AreEqual(TestConnectionProvider.OfflineStatus, updatedUserArgs.UserStatus);

                        updatedUserArgs = null;

                        await clientB.Connect();

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
                        Assert.AreEqual(TestConnectionProvider.OnlineStatus, updatedUserArgs.UserStatus);

                        clientB.Connection.IsClosed = true;

                        if (!eveUpdateReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientA never got an update :(");

                        Assert.NotNull(updatedUserArgs);

                        Assert.AreEqual(nameof(clientB), updatedUserArgs.User.Id);
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

        [TestCase]
        public async Task SendMessage_Fails_IfNoResponseNotHandledByReceiver() {
            var endpoint = new IPEndPoint(IPAddress.Loopback, 7910);

            ConnectionBase.WaitForResponseTimeout = TimeSpan.FromSeconds(60);

            using (var server = new Server(new TcpListener(endpoint), new TestConnectionProvider(), new XmlSerializer(), new TestAuthenticationHandler())) {
                server.Attach(new ServerSubscriptionModule(server, new TestChatDataProvider()));

                server.IsEnabled = true;

                using (var clientA = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientA")))
                using (var clientB = new Client(new TcpConnection(endpoint.ToString()), new XmlSerializer(), new TestAuthenticator("clientB"))) {
                    clientA.InitializeSubscriptionModule();
                    clientB.InitializeSubscriptionModule();

                    await clientA.Connect();
                    await clientB.Connect();

                    using (var eveMessageReceived = new AutoResetEvent(false)) {
                        string messageBody = null;

                        clientB.RequestReceived += (sender, args) => {
                            if (!(args.Request is Message message)) return Task.CompletedTask;

                            messageBody = message.Body;

                            // Don't set a response, this should trigger the error
                            //args.Response = new ResponsePacket(args.Request);

                            eveMessageReceived.Set();
                            return Task.CompletedTask;
                        };

                        var sendTask = clientA.SendMessage(clientB.User.Id, "asdf");

                        if (!eveMessageReceived.WaitOne(MaxTimeout))
                            Assert.Fail("clientB never got their message :(");

                        try {
                            var result = await sendTask;
                        } catch (ErrorResponseException ex) {
                            Assert.AreEqual(ErrorResponseCodes.UnhandledRequest, ex.Code);
                            return;
                        }

                        Assert.Fail("SendMessage should have failed due to no response being sent back");
                    }
                }
            }
        }
    }

    public class TestConnectionProvider : IConnectionProvider, IDisposable
    {
        public const string OnlineStatus = nameof(OnlineStatus);
        public const string OfflineStatus = nameof(OfflineStatus);

        private UserConnectionMap OnlineUsers { get; }

        private Server _server;

        public TestConnectionProvider() {
            OnlineUsers = new UserConnectionMap();
            OnlineUsers.UserConnectionChanged += OnlineUsers_UserConnectionChanged;
        }

        private async void OnlineUsers_UserConnectionChanged(object sender, UserConnectionChangedEventArgs e) {
            try {
                await _server.UpdateUserStatus(e.User, e.IsConnected ? TestConnectionProvider.OnlineStatus : TestConnectionProvider.OfflineStatus);
            } catch (ObjectDisposedException) {
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

        public User GetUser(IConnection connection) {
            return OnlineUsers.GetUser(connection);
        }

        public Task AddConnection(IConnection connection, User user) {
            return OnlineUsers.AddConnection(connection, user);
        }

        public string GetUserStatus(string userId) {
            return OnlineUsers.GetConnections(userId).Any() ? OnlineStatus : OfflineStatus;
        }

        public IEnumerable<IConnection> GetConnections() {
            return OnlineUsers.GetConnections();
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    OnlineUsers.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TestConnectionProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
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

        public virtual IEnumerable<string> GetUserSubscribers(string userId) {
            return Subscriptions.Where(usub => usub.Value.Any(sub => sub.UserId == userId)).Select(usub => usub.Key);
        }

        public virtual IEnumerable<UserSubscription> GetUserSubscriptions(string userId) {
            if(!Subscriptions.TryGetValue(userId, out IList<UserSubscription> subscriptions)) {
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

            return Task.FromResult(AuthenticationResult.Success(new User(userId, userId)));
        }
    }

    public class TestServerModule : IServerModule
    {
        public event EventHandler<RequestReceivedEventArgs> Request;

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            Request?.Invoke(sender, args);
            return Task.CompletedTask;
        }

        public Task UserStatucChanged(object sender, UserStatusChangedEventArgs e) {
            return Task.CompletedTask;
        }
    }
}
