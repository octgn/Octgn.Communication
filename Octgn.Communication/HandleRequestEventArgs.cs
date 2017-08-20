using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public class HandleRequestEventArgs : RequestPacketReceivedEventArgs
    {
        public bool IsHandled { get; set; }

        public ResponsePacket Response { get; set; }

        public HandleRequestEventArgs() {

        }

        public HandleRequestEventArgs(RequestPacketReceivedEventArgs e) {
            this.Connection = e.Connection;
            this.Packet = e.Packet;
        }
    }
}
