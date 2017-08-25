using System;
using System.Threading.Tasks;
using Octgn.Communication.Packets;
using System.Linq;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Chat
{
    public class ChatServerModule : IServerModule
    {
        private readonly IDataProvider _dataProvider;

        public ChatServerModule(Server server, IDataProvider dataProvider) {
            _dataProvider = dataProvider ?? throw new ArgumentNullException(nameof(dataProvider));
            _server = server ?? throw new ArgumentNullException(nameof(server));

            _dataProvider.UserSubscriptionUpdated += _dataProvider_UserSubscriptionUpdated;

            _requestHandler.Register(nameof(IClientCalls.GetUserSubscriptions), OnGetUserSubscriptions);
            _requestHandler.Register(nameof(IClientCalls.UpdateUserSubscription), OnUpdateUserSubscription);
            _requestHandler.Register(nameof(IClientCalls.RemoveUserSubscription), OnRemoveUserSubscription);
            _requestHandler.Register(nameof(IClientCalls.AddUserSubscription), OnAddUserSubscription);

            if(_server.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
            }
        }

        private readonly Server _server;
        private async void _dataProvider_UserSubscriptionUpdated(object sender, UserSubscriptionUpdatedEventArgs args) {
            try {
                var subscriberUsername = args.Subscription.Subscriber;
                var subscription = args.Subscription;
                var userUsername = args.Subscription.User;

                var subscriber = _server.UserProvider.GetUser(subscriberUsername);
                var user = _server.UserProvider.GetUser(userUsername);
                var connections = _server.UserProvider.GetConnections(subscriberUsername);

                foreach (var connection in connections) {
                    var packet = new RequestPacket(nameof(IServerCalls.UserSubscriptionUpdated)) {
                        ["user"] = user
                    };

                    UserSubscription.AddToPacket(packet, subscription);

                    try {
                        await connection.Request(packet);
                    } catch (Exception ex) {
                        Signal.Exception(ex);
                    }
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private Task<ResponsePacket> OnGetUserSubscriptions(RequestContext context, RequestPacket packet) {
            var subs = _dataProvider.GetUserSubscriptions(context.User.NodeId).ToArray();
            return Task.FromResult(new ResponsePacket(packet, subs));
        }

        private Task<ResponsePacket> OnUpdateUserSubscription(RequestContext context, RequestPacket packet) {
            var sub = UserSubscription.GetFromPacket(packet);

            // No other values are valid, and could potentially be malicious.
            sub.Subscriber = context.User.NodeId;

            _dataProvider.UpdateUserSubscription(sub);
            return Task.FromResult(new ResponsePacket(packet, sub));
        }

        private Task<ResponsePacket> OnRemoveUserSubscription(RequestContext context, RequestPacket packet) {
            var subid = UserSubscription.GetIdFromPacket(packet);

            if (string.IsNullOrWhiteSpace(subid)) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ResponsePacket(packet, errorData));
            }

            var sub = _dataProvider.GetUserSubscription(subid);

            if (sub?.Subscriber != context.User.NodeId) {
                var errorData = new ErrorResponseData(ErrorResponseCodes.UserSubscriptionNotFound, $"The {nameof(UserSubscription)} with the id '{subid}' was not found.", false);
                return Task.FromResult(new ResponsePacket(packet, errorData));
            }

            _dataProvider.RemoveUserSubscription(sub.Id);

            return Task.FromResult(new ResponsePacket(packet));
        }

        private Task<ResponsePacket> OnAddUserSubscription(RequestContext context, RequestPacket packet) {
            var sub = UserSubscription.GetFromPacket(packet);

            // No other values are valid, and could potentially be malicious.
            sub.Id = null;
            sub.UpdateType = UpdateType.Add;
            sub.Subscriber = context.User.NodeId;

            _dataProvider.AddUserSubscription(sub);

            return Task.FromResult(new ResponsePacket(packet, sub));
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, HandleRequestEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }

        public async Task UserChanged(object sender, UserChangedEventArgs e) {
            var subscribedUsers = _dataProvider.GetUserSubscribers(e.User.NodeId);

            foreach(var user in subscribedUsers) {
                foreach(var connection in _server.UserProvider.GetConnections(user)) {
                    var packet = new RequestPacket(nameof(IServerCalls.UserUpdated)) {
                        ["user"] = e.User
                    };

                    await connection.Request(packet);
                }
            }
        }
    }

    public static class ErrorResponseCodes
    {
        public const string UserSubscriptionNotFound = nameof(UserSubscriptionNotFound);
    }
}
