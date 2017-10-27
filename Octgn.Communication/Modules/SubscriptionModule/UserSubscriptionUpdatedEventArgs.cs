using System;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class UserSubscriptionUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public UserSubscription Subscription { get; set; }
    }
}
