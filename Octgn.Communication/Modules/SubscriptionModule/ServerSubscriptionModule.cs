using System;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class ServerSubscriptionModule : Module
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(ServerSubscriptionModule));
#pragma warning restore IDE1006 // Naming Styles

        private readonly IDataProvider _dataProvider;

        public ServerSubscriptionModule(Server server, IDataProvider dataProvider) {
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _server = server ?? throw new ArgumentNullException(nameof(server));

            _dataProvider.UserSubscriptionUpdated += _dataProvider_UserSubscriptionUpdated;

            RequestHandler requestHandler;

            Attach(requestHandler = new RequestHandler());

            requestHandler.Register(nameof(IClientCalls.GetUserSubscriptions), OnGetUserSubscriptions);
            requestHandler.Register(nameof(IClientCalls.UpdateUserSubscription), OnUpdateUserSubscription);
            requestHandler.Register(nameof(IClientCalls.RemoveUserSubscription), OnRemoveUserSubscription);
            requestHandler.Register(nameof(IClientCalls.AddUserSubscription), OnAddUserSubscription);

            if(server.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
            }

            _server.ConnectionProvider.ConnectionStateChanged += ConnectionProvider_ConnectionStateChanged;
        }

        private readonly Server _server;
        private async void _dataProvider_UserSubscriptionUpdated(object sender, UserSubscriptionUpdatedEventArgs args) {
            try {
                var subscriberUsername = args.Subscription.SubscriberUserId;
                var subscription = args.Subscription;
                var userId = args.Subscription.UserId;

                var userConnection = _server.ConnectionProvider
                    .GetConnections(userId)
                    .Where(con => con.State == ConnectionState.Connected)
                    .FirstOrDefault();

                User user = null;

                if (userConnection != null)
                    user = userConnection.User;
                else user = new User(userId, null);

                var subUpdatePacket = new RequestPacket(nameof(IServerCalls.UserSubscriptionUpdated)) {
                    ["user"] = user,
                };
                UserSubscription.AddToPacket(subUpdatePacket, subscription);
                if (subscription.UpdateType == UpdateType.Add || subscription.UpdateType == UpdateType.Update) {
                    subUpdatePacket["userStatus"] = _dataProvider.GetUserStatus(userId);
                }

                try {
                    await _server.Request(subUpdatePacket, subscriberUsername);
                } catch (ErrorResponseException ex) {
                    Log.Warn(ex);
                } catch (Exception ex) {
                    Signal.Exception(ex);
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private Task<ProcessResult> OnGetUserSubscriptions(RequestPacket request) {
            var subs = _dataProvider.GetUserSubscriptions(request.Context.User.Id).ToArray();
            return Task.FromResult(new ProcessResult(subs));
        }

        private Task<ProcessResult> OnUpdateUserSubscription(RequestPacket request) {
            var sub = UserSubscription.GetFromPacket(request);

            // No other values are valid, and could potentially be malicious.
            sub.SubscriberUserId = request.Context.User.Id;

            _dataProvider.UpdateUserSubscription(sub);

            return Task.FromResult(new ProcessResult(sub));
        }

        private Task<ProcessResult> OnRemoveUserSubscription(RequestPacket request) {
            var subid = UserSubscription.GetIdFromPacket(request);

            if (string.IsNullOrWhiteSpace(subid)) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ProcessResult(errorData));
            }

            var sub = _dataProvider.GetUserSubscription(subid);

            if (sub?.SubscriberUserId != request.Context.User.Id) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ProcessResult(errorData));
            }

            _dataProvider.RemoveUserSubscription(sub.Id);

            return Task.FromResult(ProcessResult.Processed);
        }

        private Task<ProcessResult> OnAddUserSubscription(RequestPacket request) {
            var sub = UserSubscription.GetFromPacket(request);

            // No other values are valid, and could potentially be malicious.
            sub.Id = null;
            sub.UpdateType = UpdateType.Add;
            sub.SubscriberUserId = request.Context.User.Id;

            _dataProvider.AddUserSubscription(sub);

            return Task.FromResult(new ProcessResult(sub));
        }

        public const string UserOnlineStatus = "Online";
        public const string UserOfflineStatus = "Offline";

        private async void ConnectionProvider_ConnectionStateChanged(object sender, ConnectionStateChangedEventArgs e) {
            try {
                switch (e.NewState) {
                    case ConnectionState.Created:
                    case ConnectionState.Connecting:
                    case ConnectionState.Handshaking:
                        break;
                    case ConnectionState.Connected:
                        await Broadcast_UserStatusChanged(e.Connection.User, UserOnlineStatus);
                        break;
                    case ConnectionState.Closed:
                        await Broadcast_UserStatusChanged(e.Connection.User, UserOfflineStatus);
                        break;
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private async Task Broadcast_UserStatusChanged(User user, string status) {
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.Id)) throw new ArgumentNullException(nameof(user) + "." + nameof(user.Id));
            if (string.IsNullOrWhiteSpace(status)) throw new ArgumentNullException(nameof(status));

            var subscribedUsers = _dataProvider.GetUserSubscribers(user.Id);

            foreach (var subscriber in subscribedUsers) {
                foreach (var subscriberConnection in _server.ConnectionProvider.GetConnections(subscriber).Where(con => con.State == ConnectionState.Connected)) {
                    var packet = new RequestPacket(nameof(IServerCalls.UserStatusUpdated)) {
                        ["user"] = user,
                        ["userStatus"] = status
                    };

                    try {
                        await subscriberConnection.Request(packet);
                    } catch (NotConnectedException ex) {
                        Log.Warn(ex);
                    } catch (DisconnectedException ex) {
                        Log.Warn(ex);
                    }
                }
            }
        }
    }

    public static class ErrorResponseCodes
    {
        public const string UserSubscriptionNotFound = nameof(UserSubscriptionNotFound);
    }
}
