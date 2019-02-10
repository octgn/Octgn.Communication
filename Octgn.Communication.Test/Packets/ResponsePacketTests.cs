using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System.IO;
using System.Text;

namespace Octgn.Communication.Test.Packets
{
    public class ResponsePacketTests
    {
        [TestCase]
        public void XML_Serialization_WithResponseObject()
        {
            Serialization("asdf", new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serialization_WithResponseObject()
        {
            Serialization("asdf", new JsonSerializer());
        }

        [TestCase]
        public void XML_Serialization_WithoutResponseObject()
        {
            Serialization(null, new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serialization_WithoutResponseObject()
        {
            Serialization(null, new JsonSerializer());
        }

        public void Serialization(object obj, ISerializer serializer)
        {
            var resp = obj == null
                ? new ResponsePacket()
                : new ResponsePacket(obj);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms, Encoding.Default, true))
            using (var reader = new BinaryReader(ms, Encoding.Default, true)) {
                resp.Serialize(writer, serializer);

                ms.Position = 0;

                var r2 = new ResponsePacket();
                r2.Deserialize(reader, serializer);

                Assert.AreEqual(resp.Data, r2.Data);
            }
        }
    }
}
