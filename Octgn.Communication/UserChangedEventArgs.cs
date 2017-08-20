using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{

    public class UserChangedEventArgs : EventArgs
    {
        public bool IsHandled { get; set; }
        
        public User User { get; set; }
    }
}
