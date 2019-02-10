using System;
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
                Origin = new User("1", "name"),
                Destination = "destination",
                Sent = DateTimeOffset.Now
            };

            var bytes = SerializedPacket.Create(original, serializer);

            var sp = SerializedPacket.Read(bytes);

            var unserialized = sp.DeserializePacket(serializer);

            Assert.AreEqual(original.Sent, unserialized.Sent);
            Assert.AreEqual(original.PacketType, unserialized.PacketType);
            Assert.AreEqual(original.Origin, unserialized.Origin);
            Assert.AreEqual(original.Destination, unserialized.Destination);
            Assert.AreEqual(original.Flags, unserialized.Flags);
        }

        [TestCase]
        public void XML_Packet_ThrowsException_IfPacketIsntRegistered() => Packet_ThrowsException_IfPacketIsntRegistered(new XmlSerializer());

        [TestCase]
        public void JSON_Packet_ThrowsException_IfPacketIsntRegistered() => Packet_ThrowsException_IfPacketIsntRegistered(new JsonSerializer());

        private void Packet_ThrowsException_IfPacketIsntRegistered(ISerializer serializer) {
            var packet = new UnregisteredPacket {
            };

            try {
                SerializedPacket.Create(packet, serializer);
                Assert.Fail("Exception should have been thrown");
            } catch (UnregisteredPacketException) {
            }

            Packet.RegisterPacketType<UnregisteredPacket>();

            var serialized = SerializedPacket.Create(packet, serializer);

            Packet.UnregisterPacketType<UnregisteredPacket>();

            try {
                var ser = SerializedPacket.Read(serialized);
                ser.DeserializePacket(serializer);
                Assert.Fail("Exception should have been thrown");
            } catch (UnregisteredPacketException) {
                Assert.Pass();
            }
        }

        public class UnregisteredPacket : Packet
        {
            public override PacketFlag Flags => PacketFlag.AckRequired;

            protected override string PacketStringData => "TEST-UNREG";

            public override byte PacketType => 200;
        }
    }
}
