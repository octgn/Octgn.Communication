using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class ClientSubscriptionModule : Module
    {
        public IClientCalls RPC { get; set; }

        public ClientSubscriptionModule(Client client) {
            RPC = new ClientCalls(client);

            RequestHandler requestHandler;

            Attach(requestHandler = new RequestHandler());

            requestHandler.Register(nameof(IServerCalls.UserStatusUpdated), (RequestPacket request) => {
                var userUpdateArgs = new UserUpdatedEventArgs {
                    Client = request.Context.Client,
                    User = (User)request["user"],
                    UserStatus = (string)request["userStatus"]
                };
                UserUpdated?.Invoke(this, userUpdateArgs);
                return Task.FromResult(ProcessResult.Processed);
            });

            requestHandler.Register(nameof(IServerCalls.UserSubscriptionUpdated), async (RequestPacket request) => {
                var subscription = UserSubscription.GetFromPacket(request);

                var usubArgs = new UserSubscriptionUpdatedEventArgs {
                    Client = request.Context.Client,
                    Subscription = subscription
                };
                UserSubscriptionUpdated?.Invoke(this, usubArgs);
                if(subscription.UpdateType == UpdateType.Add) {
                    var userUpdateArgs = new UserUpdatedEventArgs {
                        Client = request.Context.Client,
                        User = (User)request["user"],
                        UserStatus = (string)request["userStatus"]
                    };
                    UserUpdated?.Invoke(this, userUpdateArgs);
                }
                return ProcessResult.Processed;
            });
        }

        public event EventHandler<UserUpdatedEventArgs> UserUpdated;

        public event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
    }

    public class UserUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public User User { get; set; }
        public string UserStatus { get; set; }
    }
}
