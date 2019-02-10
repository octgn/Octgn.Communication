using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Test.Packets
{
    [TestFixture]
    public class MessagePacketTests
    {
        [TestCase]
        public void XML_Serialization() {
            Serialization(new XmlSerializer());
        }

        public void JSON_Serialization() {
            Serialization(new JsonSerializer());
        }

        private void Serialization(ISerializer serializer) {
            var message = new Message("userb", "hi");

            var serialized = SerializedPacket.Create(message, serializer);

            var read = SerializedPacket.Read(serialized);

            var deserialized = read.DeserializePacket(serializer);

            Assert.IsInstanceOf<Message>(deserialized);
        }
    }
}
