using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class ClientSubscriptionModule : IClientModule
    {
        public IClientCalls RPC { get; set; }

        public ClientSubscriptionModule(Client client) {
            RPC = new ClientCalls(client);
            _requestHandler.Register(nameof(IServerCalls.UserStatusUpdated), OnUserStatusUpdated);
            _requestHandler.Register(nameof(IServerCalls.UserSubscriptionUpdated), OnUserSubscriptionUpdated);

            if(client.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
            }
        }

        public event EventHandler<UserUpdatedEventArgs> UserUpdated;

        private Task OnUserStatusUpdated(object sender, RequestReceivedEventArgs args) {
            var userUpdateArgs = new UserUpdatedEventArgs {
                Client = args.Context.Client,
                User = (User)args.Request["user"],
                UserStatus = (string)args.Request["userStatus"]
            };
            UserUpdated?.Invoke(this, userUpdateArgs);
            return Task.FromResult(new ResponsePacket(args.Request));
        }

        public event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        private async Task<ResponsePacket> OnUserSubscriptionUpdated(object sender, RequestReceivedEventArgs args) {
            var subscription = UserSubscription.GetFromPacket(args.Request);

            var usubArgs = new UserSubscriptionUpdatedEventArgs {
                Client = args.Context.Client,
                Subscription = subscription
            };
            UserSubscriptionUpdated?.Invoke(this, usubArgs);
            if(subscription.UpdateType == UpdateType.Add) {
                await OnUserStatusUpdated(sender, args);
            }
            return new ResponsePacket(args.Request);
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }
    }

    public class UserUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public User User { get; set; }
        public string UserStatus { get; set; }
    }
}
