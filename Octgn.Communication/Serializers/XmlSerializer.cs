using Octgn.Communication.Modules.SubscriptionModule;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using SystemXmlSerializer = System.Xml.Serialization.XmlSerializer;

namespace Octgn.Communication.Serializers
{
    public class XmlSerializer : ISerializer
    {
        private readonly ConcurrentDictionary<Type, SystemXmlSerializer> _serializers = new ConcurrentDictionary<Type, SystemXmlSerializer>();
        private SystemXmlSerializer GetSerializer(Type dataType) {
            return _serializers.GetOrAdd(dataType, (t) => new SystemXmlSerializer(t, IncludedTypes));
        }

        public XmlSerializer() {
            IncludedTypes = new Type[] {
                typeof(User),
                typeof(UserSubscription)
            };
        }

        public XmlSerializer(params Type[] types) {
            IncludedTypes = (types ?? new Type[0]).Concat(new[] { typeof(User)}).ToArray();
        }

        public Type[] IncludedTypes = new Type[0];

        public void Include(params Type[] types) {
            if (types == null) return;
            IncludedTypes = IncludedTypes.Concat(types).ToArray();
        }

        public object Deserialize(Type dataType, byte[] data) {
            var serializer = GetSerializer(dataType);

            using (var ms = new MemoryStream(data)) {
                return serializer.Deserialize(ms);
            }
        }

        public byte[] Serialize(object o) {
            var serializer = GetSerializer(o.GetType());

            using (var ms = new MemoryStream()) {
                serializer.Serialize(ms, o);
                return ms.ToArray();
            }
        }
    }
}
