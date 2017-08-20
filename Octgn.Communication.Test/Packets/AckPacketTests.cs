using NUnit.Framework;
using Octgn.Communication.Packets;
using System.IO;
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

        [TestCase]
        public void Constructor_ThrowsExceptionIfNullPacket()
        {
            try
            {
                var ackPacket = new Ack(null);
                Assert.Fail("Exception should have been thrown");
            }
            catch (ArgumentException)
            {

            }
        }
    }
}
