using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Octgn.Communication.Packets
{
    public abstract class DictionaryPacket : Packet, ICollection<KeyValuePair<string, object>>
    {
        internal IDictionary<string, object> Properties { get; set; }

        public DictionaryPacket() : this(new Dictionary<string, object>()) { }

        public DictionaryPacket(IDictionary<string, object> properties) {
            Properties = properties;
        }

        public ICollection<string> Keys => Properties.Keys;
        public ICollection<object> Values => Properties.Values;
        public int Count => Properties.Count;
        public bool IsReadOnly => Properties.IsReadOnly;
        public object this[string key] { get => Properties[key]; set => Properties[key] = value; }
        public void Add(string key, object value) => Properties.Add(key, value);
        public bool ContainsKey(string key) => Properties.ContainsKey(key);
        public bool Remove(string key) => Properties.Remove(key);
        public bool TryGetValue(string key, out object value) => Properties.TryGetValue(key, out value);
        public void Add(KeyValuePair<string, object> item) => Properties.Add(item);
        public void Clear() => Properties.Clear();
        public bool Contains(KeyValuePair<string, object> item) => Properties.Contains(item);
        public void CopyTo(KeyValuePair<string, object>[] array, int arrayIndex) => Properties.CopyTo(array, arrayIndex);
        public bool Remove(KeyValuePair<string, object> item) => Properties.Remove(item);
        public IEnumerator<KeyValuePair<string, object>> GetEnumerator() => Properties.GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator() => ((IEnumerable)Properties).GetEnumerator();

        internal override void Serialize(BinaryWriter writer, ISerializer serializer) {
            var dataBytes = serializer.Serialize(this.Select(x => new NameValuePair(x)).ToList());

            writer.Write(dataBytes.Length);
            writer.Write(dataBytes);
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer) {
            var dataLength = reader.ReadInt32();

            var data = reader.ReadBytes(dataLength);

            var items = (List<NameValuePair>)serializer.Deserialize(typeof(List<NameValuePair>), data);

            Properties = items.ToDictionary(x => x.Name, x => x.Value);
        }
    }
}
