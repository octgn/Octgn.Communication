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

            _requestHandler.Register(nameof(IClientCalls.HostGame), HostGame);
            _requestHandler.Register(nameof(IClientCalls.SignalGameStarted), SignalGameStarted);

            if(_server.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
                xmlSerializer.Include(typeof(HostedGame));
                xmlSerializer.Include(typeof(HostGameRequest));
            }
        }

        private readonly Server _server;
        private async void _dataProvider_UserSubscriptionUpdated(object sender, UserSubscriptionUpdatedEventArgs args) {
            try {
                var subscriberUsername = args.Subscription.SubscriberUserId;
                var subscription = args.Subscription;
                var userId = args.Subscription.UserId;

                var connections = _server.ConnectionProvider.GetConnections(subscriberUsername);

                foreach (var connection in connections) {
                    var subUpdatePacket = new RequestPacket(nameof(IServerCalls.UserSubscriptionUpdated)) {
                        ["userId"] = userId,
                    };

                    UserSubscription.AddToPacket(subUpdatePacket, subscription);

                    if(subscription.UpdateType == UpdateType.Add || subscription.UpdateType == UpdateType.Update) {
                        subUpdatePacket["userStatus"] = _server.ConnectionProvider.GetUserStatus(userId);
                    }

                    try {
                        await connection.Request(subUpdatePacket);
                    } catch (Exception ex) {
                        Signal.Exception(ex);
                    }
                }
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        private Task<ResponsePacket> OnGetUserSubscriptions(RequestContext context, RequestPacket packet) {
            var subs = _dataProvider.GetUserSubscriptions(context.UserId).ToArray();
            return Task.FromResult(new ResponsePacket(packet, subs));
        }

        private Task<ResponsePacket> OnUpdateUserSubscription(RequestContext context, RequestPacket packet) {
            var sub = UserSubscription.GetFromPacket(packet);

            // No other values are valid, and could potentially be malicious.
            sub.SubscriberUserId = context.UserId;

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

            if (sub?.SubscriberUserId != context.UserId) {
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
            sub.SubscriberUserId = context.UserId;

            _dataProvider.AddUserSubscription(sub);

            return Task.FromResult(new ResponsePacket(packet, sub));
        }

        private async Task<ResponsePacket> HostGame(RequestContext context, RequestPacket packet) {
            var req = HostGameRequest.GetFromPacket(packet);

            HostedGame hostedGame = null;
            foreach(var connection in this._server.ConnectionProvider.GetConnections(_dataProvider.GameServerName)) {
                var result = await connection.Request(packet);

                hostedGame = result.As<HostedGame>();
            }

            return new ResponsePacket(packet, hostedGame);
        }

        private async Task<ResponsePacket> SignalGameStarted(RequestContext context, RequestPacket packet) {
            var gameId = HostedGame.GetIdFromPacket(packet);

            foreach(var connection in this._server.ConnectionProvider.GetConnections(_dataProvider.GameServerName)) {
                await connection.Request(packet);
            }

            return new ResponsePacket(packet);
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, HandleRequestEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }

        public async Task UserStatucChanged(object sender, UserStatusChangedEventArgs e) {
            if (string.IsNullOrWhiteSpace(e.UserId)) throw new ArgumentNullException(nameof(e) + "." + nameof(e.UserId));
            if (string.IsNullOrWhiteSpace(e.Status)) throw new ArgumentNullException(nameof(e) + "." + nameof(e.Status));
            var subscribedUsers = _dataProvider.GetUserSubscribers(e.UserId);

            foreach(var user in subscribedUsers) {
                foreach(var connection in _server.ConnectionProvider.GetConnections(user)) {
                    var packet = new RequestPacket(nameof(IServerCalls.UserStatusUpdated)) {
                        ["userId"] = e.UserId,
                        ["userStatus"] = e.Status
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
