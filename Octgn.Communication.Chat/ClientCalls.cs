using Octgn.Communication;
using Octgn.Communication.Packets;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication.Chat
{
    public class ClientCalls : IClientCalls
    {
        private readonly Client _client;

        public ClientCalls(Octgn.Communication.Client client) {
            _client = client;
        }

        public async Task<IEnumerable<UserSubscription>> GetUserSubscriptions() {
            var packet = new RequestPacket(nameof(IClientCalls.GetUserSubscriptions));

            var result = await _client.Connection.Request(packet);

            return result.As<IEnumerable<UserSubscription>>();
        }

        public async Task<UserSubscription> AddUserSubscription(string name, string category) {
            var subscription = new UserSubscription {
                User = name,
                Subscriber = _client.Me.NodeId,
                Category = category
            };

            var packet = new RequestPacket(nameof(IClientCalls.AddUserSubscription));
            UserSubscription.AddToPacket(packet, subscription);

            var result = await _client.Connection.Request(packet);

            return result.As<UserSubscription>();
        }

        public async Task RemoveUserSubscription(string subscriptionId) {
            var packet = new RequestPacket(nameof(IClientCalls.RemoveUserSubscription));
            UserSubscription.AddIdToPacket(packet, subscriptionId);

            var result = await _client.Connection.Request(packet);
        }

        public async Task<UserSubscription> UpdateUserSubscription(UserSubscription subscription) {
            var packet = new RequestPacket(nameof(IClientCalls.UpdateUserSubscription));
            UserSubscription.AddToPacket(packet, subscription);

            var result = await _client.Connection.Request(packet);

            return result.As<UserSubscription>();
        }
    }
}
