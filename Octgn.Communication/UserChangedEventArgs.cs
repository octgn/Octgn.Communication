using System;

namespace Octgn.Communication
{
    public class UserStatusChangedEventArgs : EventArgs
    {
        public bool IsHandled { get; set; }

        public User User { get; set; }
        public string Status { get; set; }
    }
}
