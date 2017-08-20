using Newtonsoft.Json;
using System.Text;
using System;

namespace Octgn.Communication.Serializers
{
    public class JsonSerializer : ISerializer
    {
        private readonly static JsonSerializerSettings _jsonSettings;

        static JsonSerializer()
        {
            _jsonSettings = new JsonSerializerSettings() {
                Formatting = Formatting.None,
                PreserveReferencesHandling = PreserveReferencesHandling.All,
                ReferenceLoopHandling = ReferenceLoopHandling.Ignore
            };
        }

        public object Deserialize(Type dataType, byte[] data) {
            var dataString = Encoding.UTF8.GetString(data, 0, data.Length);
            return JsonConvert.DeserializeObject(dataString, dataType, _jsonSettings);
        }

        public byte[] Serialize(object o)
        {
            var ostring = JsonConvert.SerializeObject(o, _jsonSettings);
            return Encoding.UTF8.GetBytes(ostring);
        }
    }
}
