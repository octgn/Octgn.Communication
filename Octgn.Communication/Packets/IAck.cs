using System;

namespace Octgn.Communication.Packets
{

    public interface IAck
    {
        ulong PacketId { get; set; }
        DateTimeOffset PacketReceived { get; set; }
    }
}
