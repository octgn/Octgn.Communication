using System;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class ServerSubscriptionModule : IServerModule
    {
#pragma warning disable IDE1006 // Naming Styles
        private static readonly ILogger Log = LoggerFactory.Create(nameof(ServerSubscriptionModule));
#pragma warning restore IDE1006 // Naming Styles

        private readonly IDataProvider _dataProvider;

        public ServerSubscriptionModule(Server server, IDataProvider dataProvider) {
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _server = server ?? throw new ArgumentNullException(nameof(server));

            _dataProvider.UserSubscriptionUpdated += _dataProvider_UserSubscriptionUpdated;

            _requestHandler.Register(nameof(IClientCalls.GetUserSubscriptions), OnGetUserSubscriptions);
            _requestHandler.Register(nameof(IClientCalls.UpdateUserSubscription), OnUpdateUserSubscription);
            _requestHandler.Register(nameof(IClientCalls.RemoveUserSubscription), OnRemoveUserSubscription);
            _requestHandler.Register(nameof(IClientCalls.AddUserSubscription), OnAddUserSubscription);

            if (_server.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
            }
        }

        private readonly Server _server;
        private async void _dataProvider_UserSubscriptionUpdated(object sender, UserSubscriptionUpdatedEventArgs args) {
            try {
                var subscriberUsername = args.Subscription.SubscriberUserId;
                var subscription = args.Subscription;
                var userId = args.Subscription.UserId;

                var userConnection = _server.ConnectionProvider.GetConnections(userId).FirstOrDefault();

                User user = null;

                if (userConnection != null)
                    user = _server.ConnectionProvider.GetUser(userConnection);
                else user = new User(userId, null);

                var subUpdatePacket = new RequestPacket(nameof(IServerCalls.UserSubscriptionUpdated)) {
                    ["user"] = user,
                };
                UserSubscription.AddToPacket(subUpdatePacket, subscription);
                if (subscription.UpdateType == UpdateType.Add || subscription.UpdateType == UpdateType.Update) {
                    subUpdatePacket["userStatus"] = _server.ConnectionProvider.GetUserStatus(userId);
                }

                try {
                    await _server.Request(subUpdatePacket, subscriberUsername);
                } catch (Exception ex) {
                    Signal.Exception(ex);
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private Task OnGetUserSubscriptions(object sender, RequestReceivedEventArgs args) {
            var subs = _dataProvider.GetUserSubscriptions(args.Context.User.Id).ToArray();
            return Task.FromResult(new ResponsePacket(args.Request, subs));
        }

        private Task OnUpdateUserSubscription(object sender, RequestReceivedEventArgs args) {
            var sub = UserSubscription.GetFromPacket(args.Request);

            // No other values are valid, and could potentially be malicious.
            sub.SubscriberUserId = args.Context.User.Id;

            _dataProvider.UpdateUserSubscription(sub);

            return Task.FromResult(new ResponsePacket(args.Request, sub));
        }

        private Task OnRemoveUserSubscription(object sender, RequestReceivedEventArgs args) {
            var subid = UserSubscription.GetIdFromPacket(args.Request);

            if (string.IsNullOrWhiteSpace(subid)) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ResponsePacket(args.Request, errorData));
            }

            var sub = _dataProvider.GetUserSubscription(subid);

            if (sub?.SubscriberUserId != args.Context.User.Id) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ResponsePacket(args.Request, errorData));
            }

            _dataProvider.RemoveUserSubscription(sub.Id);

            return Task.FromResult(new ResponsePacket(args.Request));
        }

        private Task OnAddUserSubscription(object sender, RequestReceivedEventArgs args) {
            var sub = UserSubscription.GetFromPacket(args.Request);

            // No other values are valid, and could potentially be malicious.
            sub.Id = null;
            sub.UpdateType = UpdateType.Add;
            sub.SubscriberUserId = args.Context.User.Id;

            _dataProvider.AddUserSubscription(sub);

            return Task.FromResult(new ResponsePacket(args.Request, sub));
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }

        public async Task UserStatusChanged(object sender, UserStatusChangedEventArgs e) {
            if (e.User == null) throw new ArgumentNullException(nameof(e) + "." + nameof(e.User));
            if (string.IsNullOrWhiteSpace(e.User.Id)) throw new ArgumentNullException(nameof(e) + "." + nameof(e.User) + "." + nameof(e.User.Id));
            if (string.IsNullOrWhiteSpace(e.Status)) throw new ArgumentNullException(nameof(e) + "." + nameof(e.Status));
            var subscribedUsers = _dataProvider.GetUserSubscribers(e.User.Id);

            foreach (var user in subscribedUsers) {
                foreach (var connection in _server.ConnectionProvider.GetConnections(user)) {
                    var packet = new RequestPacket(nameof(IServerCalls.UserStatusUpdated)) {
                        ["user"] = e.User,
                        ["userStatus"] = e.Status
                    };

                    try {
                        await connection.Request(packet);
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
