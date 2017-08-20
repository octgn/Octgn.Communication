using System;
using System.IO;
using System.Linq;
using SystemXmlSerializer = System.Xml.Serialization.XmlSerializer;

namespace Octgn.Communication.Serializers
{
    public class XmlSerializer : ISerializer
    {
        public XmlSerializer() { }

        public XmlSerializer(params Type[] types) {
            IncludedTypes = types ?? new Type[0];
        }

        public Type[] IncludedTypes = new Type[0];

        public void Include(params Type[] types) {
            if (types == null) return;
            IncludedTypes = IncludedTypes.Concat(types).ToArray();
        }

        public object Deserialize(Type dataType, byte[] data) {
            var serializer = new SystemXmlSerializer(dataType, IncludedTypes);

            using (var ms = new MemoryStream(data)) {
                return serializer.Deserialize(ms);
            }
        }

        public byte[] Serialize(object o) {
            var serializer = new SystemXmlSerializer(o.GetType(), IncludedTypes);

            using (var ms = new MemoryStream()) {
                serializer.Serialize(ms, o);
                return ms.ToArray();
            }
        }
    }
}
