using Octgn.Communication;
using System;
using System.Collections.Generic;

namespace Octgn.Communication.Chat
{

    public class UserSubscriptionUpdatedEventArgs : EventArgs
    {
        public Client Client { get; set; }
        public UserSubscription Subscription { get; set; }
    }
}
