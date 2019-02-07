﻿using Octgn.Communication.Modules.SubscriptionModule;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization;

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

            _serializers.Add(typeof(List<NameValuePair>), new DataContractSerializer(typeof(List<NameValuePair>)));
            _serializers.Add(typeof(HandshakeResult), new DataContractSerializer(typeof(HandshakeResult)));
            _serializers.Add(typeof(ErrorResponseData), new DataContractSerializer(typeof(ErrorResponseData)));
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
}
