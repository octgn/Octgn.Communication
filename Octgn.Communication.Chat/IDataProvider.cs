using Octgn.Communication;
using System;
using System.Collections.Generic;

namespace Octgn.Communication.Chat
{
    public interface IDataProvider
    {
        string GameServerName { get; set; }
        event EventHandler<UserSubscriptionUpdatedEventArgs> UserSubscriptionUpdated;
        IEnumerable<UserSubscription> GetUserSubscriptions(string user);
        void AddUserSubscription(UserSubscription subscription);
        void RemoveUserSubscription(string subscriptionId);
        void UpdateUserSubscription(UserSubscription subscription);
        IEnumerable<string> GetUserSubscribers(string user);
        UserSubscription GetUserSubscription(string subscriptionId);
    }

    public class UserSubscriptionUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public UserSubscription Subscription { get; set; }
    }
}
