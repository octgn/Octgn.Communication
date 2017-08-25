using Octgn.Communication;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication.Chat
{
    public class ChatClientModule : IClientModule
    {
        public IClientCalls RPC { get; set; }

        public ChatClientModule(Client client) {
            RPC = new ClientCalls(client);
            _requestHandler.Register(nameof(IServerCalls.UserUpdated), OnUserUpdated);
            _requestHandler.Register(nameof(IServerCalls.UserSubscriptionUpdated), OnUserSubscriptionUpdated);
            _requestHandler.Register(nameof(IServerCalls.HostedGameReady), OnHostedGameReady);
            _requestHandler.Register(nameof(Message), OnMessageReceived);

            if(client.Serializer is XmlSerializer xmlSerializer) {
                xmlSerializer.Include(typeof(UserSubscription));
                xmlSerializer.Include(typeof(HostedGame));
                xmlSerializer.Include(typeof(HostGameRequest));
            }
        }

        public event EventHandler<UserUpdatedEventArgs> UserUpdated;

        private Task<ResponsePacket> OnUserUpdated(RequestContext context, RequestPacket packet) {
            var args = new UserUpdatedEventArgs {
                Client = context.Client,
                User = (User)packet["user"]

            };
            UserUpdated?.Invoke(this, args);
            return Task.FromResult(new ResponsePacket(packet));
        }

        public event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        private async Task<ResponsePacket> OnUserSubscriptionUpdated(RequestContext context, RequestPacket packet) {
            var subscription = UserSubscription.GetFromPacket(packet);

            var args = new UserSubscriptionUpdatedEventArgs {
                Client = context.Client,
                Subscription = subscription
            };
            UserSubscriptionUpdated?.Invoke(this, args);
            if(subscription.UpdateType == UpdateType.Add) {
                await OnUserUpdated(context, packet);
            }
            return new ResponsePacket(packet);
        }

        public event EventHandler<MessageReceivedEventArgs> MessageReceived;

        private Task<ResponsePacket> OnMessageReceived(RequestContext context, RequestPacket packet) {
            var args = new MessageReceivedEventArgs {
                Client = context.Client,
                Message = (Message)packet,
            };
            MessageReceived?.Invoke(this, args);
            return Task.FromResult(new ResponsePacket(packet));
        }

        public event EventHandler<HostedGameReadyEventArgs> HostedGameReady;
        private Task<ResponsePacket> OnHostedGameReady(RequestContext context, RequestPacket packet) {
            var game = HostedGame.GetFromPacket(packet);

            var args = new HostedGameReadyEventArgs {
                Client = context.Client,
                Game = game
            };
            HostedGameReady?.Invoke(this, args);
            return Task.FromResult(new ResponsePacket(packet));
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, HandleRequestEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }
    }

    public class MessageReceivedEventArgs : RequestReceivedEventArgs
    {
        public Message Message { get; set; }
        private new RequestPacket Request { get; set; }

        public MessageReceivedEventArgs() {
        }
    }

    public class UserUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public User User { get; set; }
    }

    public class HostedGameReadyEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public HostedGame Game { get; set; }
    }
}
