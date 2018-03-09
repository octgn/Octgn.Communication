using System;
using System.Collections.Generic;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public interface IDataProvider
    {
        event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        IEnumerable<UserSubscription> GetUserSubscriptions(string userId);
        void AddUserSubscription(UserSubscription subscription);
        void RemoveUserSubscription(string subscriptionId);
        void UpdateUserSubscription(UserSubscription subscription);
        IEnumerable<string> GetUserSubscribers(string userId);
        UserSubscription GetUserSubscription(string subscriptionId);
        void SetUserStatus(string userId, string status);
        string GetUserStatus(string userId);
    }
}
