using NUnit.Framework;
using Octgn.Communication;
using Octgn.Communication.Serializers;
using System.Linq;

namespace Octgn.Communication.Chat.Test
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
            message.Id = 1;

            var serialized = Packet.Serialize(message, serializer).ToList();

            var deserialized = Packet.Deserialize(serialized, serializer, out int bytesUsed);

            Assert.IsInstanceOf<Message>(deserialized);
        }
    }
}
