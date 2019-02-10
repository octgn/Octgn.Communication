using NUnit.Framework;
using Octgn.Communication.Packets;
using System;

namespace Octgn.Communication.Test.Packets
{
    [TestFixture]
    public class AckPacketTests
    {
        [TestCase]
        public void PacketToString_NotNullOrWhitespace()
        {
            var packet = new Ack();

            Assert.False(string.IsNullOrWhiteSpace(packet.ToString()));
        }
    }
}
