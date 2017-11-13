using System;

namespace Octgn.Communication
{
    public class UserConnectionChangedEventArgs : EventArgs
    {
        public User User { get; set; }
        public bool IsConnected { get; set; }
    }
}
