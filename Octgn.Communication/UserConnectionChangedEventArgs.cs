using System;

namespace Octgn.Communication
{

    public class UserConnectionChangedEventArgs : EventArgs
    {
        public string UserId { get; set; }
        public bool IsConnected { get; set; }
    }
}
