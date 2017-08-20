using NUnit.Framework;
using Octgn.Communication.Packets;
using System.IO;
using System;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Test.Packets
{
    public class RequestPacketTests
    {
        [TestCase]
        public void XML_Serialization()
        {
            Serialization(new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serialization()
        {
            Serialization(new JsonSerializer());
        }

        private void Serialization(ISerializer serializer)
        {
            var req = new RequestPacket("asdf");

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            using (var reader = new BinaryReader(ms)) {

                req.Serialize(writer, serializer);

                ms.Position = 0;

                var r2 = new RequestPacket("name");
                r2.Deserialize(reader, serializer);

                Assert.AreEqual(req.Name, r2.Name);
            }
        }
    }
}
