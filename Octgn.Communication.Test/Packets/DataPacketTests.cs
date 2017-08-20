using NUnit.Framework;
using Octgn.Communication.Packets;
using System.IO;
using System;
using Octgn.Communication.Serializers;

namespace Octgn.Communication.Test.Packets
{
    public class DataPacketTests
    {
        [TestCase]
        public void XML_Serialization()
        {
            SerializationTest("asdf", new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serialization()
        {
            SerializationTest("asdf", new JsonSerializer());
        }

        [TestCase]
        public void XML_Serialization_NullObject()
        {
            SerializationTest(null, new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serialization_NullObject()
        {
            SerializationTest(null, new JsonSerializer());
        }

        private void SerializationTest(object obj, ISerializer serializer) {
            var pack = new TestDataPacket(obj);

            var processed = Serialize(pack, serializer);

            Assert.AreEqual(obj, processed.Data);
            Assert.AreEqual(pack.Data, processed.Data);
            Assert.AreEqual(obj?.GetType().AssemblyQualifiedName ?? "NULL", processed.DataType);
        }

        public T Serialize<T>(T packet, ISerializer serializer) where T : Packet {
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            using (var reader = new BinaryReader(ms)) {

                packet.Serialize(writer, serializer);

                ms.Position = 0;

                var ret = Activator.CreateInstance<T>();
                ret.Deserialize(reader, serializer);

                return ret;
            }
        }

        [TestCase]
        public void XML_Serializes_ErrorResponseData() {
            Serializes_ErrorResponseData(new XmlSerializer());
        }

        [TestCase]
        public void JSON_Serializes_ErrorResponseData() {
            Serializes_ErrorResponseData(new JsonSerializer());
        }

        private void Serializes_ErrorResponseData(ISerializer serializer) {

            var error = new ErrorResponseData(ErrorResponseCodes.UnauthorizedRequest, "crap", true);

            var req = new RequestPacket("hi") {
                Id = 1
            };

            var pack = new ResponsePacket(req, error);

            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms))
            using (var reader = new BinaryReader(ms)) {

                pack.Serialize(writer, serializer);

                ms.Position = 0;

                var p2 = new TestDataPacket();
                p2.Deserialize(reader, serializer);

                Assert.AreEqual(error.Code, p2.As<ErrorResponseData>().Code);
                Assert.AreEqual(error.Message, p2.As<ErrorResponseData>().Message);
                Assert.AreEqual(error.IsCritical, p2.As<ErrorResponseData>().IsCritical);
                Assert.AreEqual(error.GetType(), p2.Data.GetType());
                Assert.AreEqual(typeof(ErrorResponseData).AssemblyQualifiedName, p2.DataType);
            }
        }

        [TestCase]
        public void DataPacket_DataTypeGetsSet()
        {
            var pack = new TestDataPacket("asdf");
            Assert.AreEqual(typeof(string).AssemblyQualifiedName, pack.DataType);
        }

        [TestCase]
        public void As_ThrowsInvalidCastIfCastIsIncorrect()
        {
            var dp = new TestDataPacket("asdf");
            try
            {
                dp.As<IConnectionListener>();
                Assert.Fail("Exception should have been thrown");
            }
#pragma warning disable CS0168 // Variable is declared but never used
            catch (InvalidCastException ex)
#pragma warning restore CS0168 // Variable is declared but never used
            {
            }
        }

        public class TestDataPacket : DataPacket
        {
            public TestDataPacket()
            {
            }

            public TestDataPacket(object o) : base(o)
            {

            }

            protected override string PacketStringData => "TEST";

            internal override byte PacketTypeId => 7;
        }
    }
}
