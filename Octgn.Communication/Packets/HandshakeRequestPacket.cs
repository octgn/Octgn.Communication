using System;
using System.Collections.Generic;

namespace Octgn.Communication.Packets
{
    public class HandshakeRequestPacket : RequestPacket
    {
        public string HandshakeType {
            get => (string)this[nameof(HandshakeType)];
            set => this[nameof(HandshakeType)] = value;
        }

        public override byte PacketType => 5;

        public HandshakeRequestPacket() {

        }

        public HandshakeRequestPacket(string handshakeType) : this(handshakeType, new Dictionary<string, object>()) {

        }

        public HandshakeRequestPacket(string handshakeType, IDictionary<string, object> properties)
            : base(nameof(HandshakeRequestPacket),
              properties ?? new Dictionary<string, object>()) {
            if (string.IsNullOrWhiteSpace(handshakeType))
                throw new ArgumentException("Can't be blank", nameof(handshakeType));

            HandshakeType = handshakeType;
        }
    }
}
