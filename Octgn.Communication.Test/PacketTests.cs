using System;
using System.Linq;
using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Test
{
    [TestFixture]
    public class PacketTests : TestBase
    {
        [TestCase]
        public void XML_Serialization() => Serialization(new XmlSerializer());

        [TestCase]
        public void JSON_Serialization() => Serialization(new JsonSerializer());

        private void Serialization(ISerializer serializer) {
            const string test = "test";
            var original = new RequestPacket(test) {
                Id = 1
            };

            var bytes = Packet.Serialize(original, serializer);

            var unserialized = (RequestPacket)Packet.Deserialize(bytes.ToList(), serializer, out int bytesConsumed);

            Assert.AreEqual(bytes.Length, bytesConsumed);

            Assert.AreEqual(original.Id, unserialized.Id);
            Assert.AreEqual(original.Sent, unserialized.Sent);
            Assert.AreEqual(original.PacketTypeId, unserialized.PacketTypeId);
        }

        [TestCase]
        public void XML_Serialization_ThrowsException_WhenNoIdPresent() => Serialization_ThrowsException_WhenNoIdPresent(new XmlSerializer());

        [TestCase]
        public void JSON_Serialization_ThrowsException_WhenNoIdPresent() => Serialization_ThrowsException_WhenNoIdPresent(new JsonSerializer());

        private void Serialization_ThrowsException_WhenNoIdPresent(ISerializer serializer) {
            var packet = new RequestPacket();

            try {
                Packet.Serialize(packet, serializer);
                Assert.Fail();
            } catch (ArgumentException ex) {
                Assert.AreEqual(nameof(packet), ex.ParamName);
            }
        }

        [TestCase]
        public void XML_Packet_ThrowsException_IfPacketIsntRegistered() => Packet_ThrowsException_IfPacketIsntRegistered(new XmlSerializer());

        [TestCase]
        public void JSON_Packet_ThrowsException_IfPacketIsntRegistered() => Packet_ThrowsException_IfPacketIsntRegistered(new JsonSerializer());

        private void Packet_ThrowsException_IfPacketIsntRegistered(ISerializer serializer) {
            var packet = new UnregisteredPacket {
                Id = 1
            };

            try {
                Packet.Serialize(packet, serializer);
                Assert.Fail("Exception should have been thrown");
            } catch (UnregisteredPacketException) {
            }

            Packet.RegisterPacketType<UnregisteredPacket>();

            var serialized = Packet.Serialize(packet, serializer);

            Packet.UnregisterPacketType<UnregisteredPacket>();

            try {
                Packet.Deserialize(serialized.ToList(), serializer, out _);
                Assert.Fail("Exception should have been thrown");
            } catch (UnregisteredPacketException) {
                Assert.Pass();
            }
        }

        public class UnregisteredPacket : Packet
        {
            public override bool RequiresAck => true;

            protected override string PacketStringData => "TEST-UNREG";

            public override byte PacketTypeId => 200;
        }
    }
}
