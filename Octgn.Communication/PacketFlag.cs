using System;
using System.IO;

namespace Octgn.Communication
{
    [Flags]
    public enum PacketFlag : byte
    {
        None = 0,
        AckRequired = 1
    }
}
