using System;

namespace Octgn.Communication
{
    public interface IPacket
    {
        byte PacketType { get; }

        PacketFlag Flags { get; }

        string Destination { get; }

        User Origin { get; }

        DateTimeOffset Sent { get; }
    }
}
