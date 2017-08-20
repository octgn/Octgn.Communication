﻿using NUnit.Framework;
using Octgn.Communication.Packets;
using Octgn.Communication.Serializers;
using System.IO;

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
            var reqPacket = new RequestPacket("name") { Id = 99 };

            var resp = obj == null
                ? new ResponsePacket(reqPacket)
                : new ResponsePacket(reqPacket, obj);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            using (var reader = new BinaryReader(ms)) {

                resp.Serialize(writer, serializer);

                ms.Position = 0;

                var r2 = new ResponsePacket();
                r2.Deserialize(reader, serializer);

                Assert.AreEqual(resp.Data, r2.Data);
            }
        }
    }
}
