using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octgn.Communication.Packets;

namespace Octgn.Communication
{

    public class RequestContext
    {
        public IConnection Connection { get; set; }
        public User User { get; set; }
        public Server Server { get; set; }
        public Client Client { get; set; }
    }
}
