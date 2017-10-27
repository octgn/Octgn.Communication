using Octgn.Communication;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public interface IServerCalls
    {
        Task UserStatusUpdated(string userId, string userStatus);
        Task UserSubscriptionUpdated(UserSubscription subscription);
    }
}
