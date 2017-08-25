using Octgn.Communication;
using System.Threading.Tasks;

namespace Octgn.Communication.Chat
{
    public interface IServerCalls
    {
        Task UserUpdated(User user);
        Task UserSubscriptionUpdated(UserSubscription subscription);
        Task HostedGameReady(HostedGame data);
    }
}
