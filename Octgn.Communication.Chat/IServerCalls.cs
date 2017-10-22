using Octgn.Communication;
using System.Threading.Tasks;

namespace Octgn.Communication.Chat
{
    public interface IServerCalls
    {
        Task UserStatusUpdated(string userId, string userStatus);
        Task UserSubscriptionUpdated(UserSubscription subscription);
        Task HostedGameReady(HostedGame data);
    }
}
