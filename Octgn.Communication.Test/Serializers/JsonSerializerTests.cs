using System;
using NUnit.Framework;
using Octgn.Communication.Serializers;
using System.Text;
using Octgn.Communication.Packets;

namespace Octgn.Communication.Test.Serializers
{
    [TestFixture]
    public class JsonSerializerTests : TestBase
    {
        [TestCase]
        public void Serialize_Works() {
            var serializer = new JsonSerializer();
            var packet = new RequestPacket("test") {
                ["test2"] = "test3"
            };

            var serialized = serializer.Serialize(packet);

            var str = Encoding.UTF8.GetString(serialized);
            Console.WriteLine(str);

            var unserialized = (RequestPacket)serializer.Deserialize(typeof(RequestPacket), serialized);

            Assert.NotNull(unserialized);

            Assert.AreEqual("test", unserialized.Name);
            Assert.AreEqual("test3", unserialized["test2"]);
        }
    }
}
