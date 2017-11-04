﻿using Octgn.Communication.Packets;
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

        private Task<ResponsePacket> OnUserStatusUpdated(RequestContext context, RequestPacket packet) {
            var args = new UserUpdatedEventArgs {
                Client = context.Client,
                UserId = (string)packet["userId"],
                UserStatus = (string)packet["userStatus"]
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
                await OnUserStatusUpdated(context, packet);
            }
            return new ResponsePacket(packet);
        }

        private readonly RequestHandler _requestHandler = new RequestHandler();

        public Task HandleRequest(object sender, HandleRequestEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }
    }

    public class UserUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public string UserId { get; set; }
        public string UserStatus { get; set; }
    }
}