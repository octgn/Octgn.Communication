using Octgn.Communication.Modules.SubscriptionModule;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using SystemXmlSerializer = System.Xml.Serialization.XmlSerializer;

namespace Octgn.Communication.Serializers
{
    public class XmlSerializer2 : ISerializer
    {
        private readonly Dictionary<Type, SystemXmlSerializer> _serializers = new Dictionary<Type, SystemXmlSerializer>();
        private SystemXmlSerializer GetSerializer(Type dataType) {
            if (_serializers.TryGetValue(dataType, out var serializer)) {
                return serializer;
            } else throw new InvalidOperationException($"No serializer found for type {dataType.Name}");
        }

        public XmlSerializer2() {
            IncludedTypes = new Type[] {
                typeof(User),
                typeof(UserSubscription)
            };

            _serializers.Add(typeof(List<NameValuePair>), new SystemXmlSerializer(typeof(List<NameValuePair>)));
            _serializers.Add(typeof(HandshakeResult), new SystemXmlSerializer(typeof(HandshakeResult)));
            _serializers.Add(typeof(ErrorResponseData), new SystemXmlSerializer(typeof(ErrorResponseData)));
            _serializers.Add(typeof(DateTime), new SystemXmlSerializer(typeof(DateTime)));
            _serializers.Add(typeof(string), new SystemXmlSerializer(typeof(string)));
        }

        public XmlSerializer2(params Type[] types) {
            IncludedTypes = (types ?? new Type[0]).Concat(new[] { typeof(User) }).ToArray();
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
