using Octgn.Communication;
using System.Collections.Generic;

namespace Octgn.Communication.Chat
{
    public class Room
    {
        public string NodeId { get; set; }
        public List<User> Users { get; set; }
    }
}
