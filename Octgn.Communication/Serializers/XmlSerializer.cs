using Octgn.Communication.Modules.SubscriptionModule;
using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;
using System.Xml;

namespace Octgn.Communication.Serializers
{
    public class XmlSerializer : ISerializer
    {
        private readonly Dictionary<Type, DataContractSerializer> _serializers = new Dictionary<Type, DataContractSerializer>();
        private DataContractSerializer GetSerializer(Type dataType) {
            if(_serializers.TryGetValue(dataType, out var serializer)) {
                return serializer;
            } else throw new InvalidOperationException($"No serializer found for type {dataType.Name}");
        }

        public XmlSerializer() {
            IncludedTypes = new Type[] {
                typeof(User),
                typeof(UserSubscription)
            };


            var settings = new DataContractSerializerSettings();
            settings.DataContractResolver = new XmlDataContractResolver(this);

            _serializers.Add(typeof(List<NameValuePair>), new DataContractSerializer(typeof(List<NameValuePair>), settings));
            _serializers.Add(typeof(HandshakeResult), new DataContractSerializer(typeof(HandshakeResult), settings));
            _serializers.Add(typeof(ErrorResponseData), new DataContractSerializer(typeof(ErrorResponseData), settings));
            _serializers.Add(typeof(DateTime), new DataContractSerializer(typeof(DateTime), settings));
            _serializers.Add(typeof(string), new DataContractSerializer(typeof(string), settings));
            _serializers.Add(typeof(UserSubscription), new DataContractSerializer(typeof(UserSubscription), settings));
            _serializers.Add(typeof(ResponsePacket), new DataContractSerializer(typeof(ResponsePacket), settings));
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
                return serializer.ReadObject(ms);
            }
        }

        public byte[] Serialize(object o) {
            var serializer = GetSerializer(o.GetType());

            using (var ms = new MemoryStream()) {
                serializer.WriteObject(ms, o);
                return ms.ToArray();
            }
        }
    }

    public class XmlDataContractResolver : DataContractResolver
    {
        private readonly XmlSerializer _serializer;

        public XmlDataContractResolver(XmlSerializer serializer) {
            _serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }

        public override Type ResolveName(string typeName, string typeNamespace, Type declaredType, DataContractResolver knownTypeResolver) {
            var result = knownTypeResolver.ResolveName(typeName, typeNamespace, declaredType, knownTypeResolver);
            if (result != null) return result;

            return _serializer.IncludedTypes.FirstOrDefault(x => x.Name == typeName);
        }

        public override bool TryResolveType(Type type, Type declaredType, DataContractResolver knownTypeResolver, out XmlDictionaryString typeName, out XmlDictionaryString typeNamespace) {
            if (knownTypeResolver.TryResolveType(type, declaredType, knownTypeResolver, out typeName, out typeNamespace))
                return true;

            throw new NotImplementedException();
        }
    }
}
