using System;
using System.Collections.Generic;
using System.IO;

namespace Octgn.Communication.Packets
{
    public class RequestPacket : DictionaryPacket
    {
        public string Name {
            get => (string)this["name"];
            set => this["name"] = value;
        }

        public RequestPacket() {

        }

        public RequestPacket(string name) : this(name, new Dictionary<string, object>()) {

        }

        public RequestPacket(string name, IDictionary<string, object> properties) : base(properties ?? new Dictionary<string, object>()) {
            if (string.IsNullOrWhiteSpace(name))
                throw new ArgumentException("Can't be blank", nameof(name));
            Name = name;
        }

        public RequestPacket(RequestPacket request)
            : this(request.Name, request.Properties) {
            this.Origin = request.Origin;
            this.Destination = request.Destination;
            this._packetTypeId = request.PacketTypeId;
        }

        public override byte PacketTypeId => _packetTypeId;
        private byte _packetTypeId = 3;

        public override bool RequiresAck => true;

        protected override string PacketStringData => $"REQ+{Name}";

        internal override void Serialize(BinaryWriter writer, ISerializer serializer) {
            base.Serialize(writer, serializer);

            writer.Write(Name ?? string.Empty);
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer) {
            base.Deserialize(reader, serializer);

            Name = reader.ReadString();
        }
    }
}
