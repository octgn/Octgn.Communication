using Octgn.Communication;
using System;
using System.Collections.Generic;

namespace Octgn.Communication.Chat
{
    public interface IDataProvider
    {
        event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        IEnumerable<UserSubscription> GetUserSubscriptions(string user);
        void AddUserSubscription(UserSubscription subscription, string user);
        void RemoveUserSubscription(string subscriptionId, string user);
        void UpdateUserSubscription(UserSubscription subscription, string user);
        IEnumerable<string> GetUserSubscribers(string user);
    }

    public class UserSubscriptionUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public UserSubscription Subscription { get; set; }
    }
}
