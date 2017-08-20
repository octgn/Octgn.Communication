using System.Collections.Generic;

namespace Octgn.Communication
{
    public class NameValuePair
    {
        public string Name { get; set; }
        public object Value { get; set; }

        public NameValuePair() { }
        public NameValuePair(string name, object val) {
            Name = name;
            Value = val;
        }

        public NameValuePair(KeyValuePair<string, object> x) {
            Name = x.Key;
            Value = x.Value;
        }
    }
}
