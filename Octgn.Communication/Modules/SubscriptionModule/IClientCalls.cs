using Octgn.Communication;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public interface IClientCalls
    {
        Task<IEnumerable<UserSubscription>> GetUserSubscriptions();

        Task<UserSubscription> AddUserSubscription(string name, string category);

        Task RemoveUserSubscription(string subscriptionId);

        Task<UserSubscription> UpdateUserSubscription(UserSubscription subscription);
    }
}
