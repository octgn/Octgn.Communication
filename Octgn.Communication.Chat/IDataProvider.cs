using System;
using System.Collections.Generic;

namespace Octgn.Communication.Chat
{
    public interface IDataProvider
    {
        string GameServerName { get; set; }
        event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        IEnumerable<UserSubscription> GetUserSubscriptions(string userId);
        void AddUserSubscription(UserSubscription subscription);
        void RemoveUserSubscription(string subscriptionId);
        void UpdateUserSubscription(UserSubscription subscription);
        IEnumerable<string> GetUserSubscribers(string userId);
        UserSubscription GetUserSubscription(string subscriptionId);
    }
}
