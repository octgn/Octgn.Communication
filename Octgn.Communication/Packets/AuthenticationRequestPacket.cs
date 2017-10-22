using System;
using System.Collections.Generic;

namespace Octgn.Communication.Packets
{
    public class AuthenticationRequestPacket : RequestPacket
    {
        public string AuthenticationType { get; set; }
        internal override byte PacketTypeId => 5;

        public AuthenticationRequestPacket() {

        }

        public AuthenticationRequestPacket(string authenticationType) : this(authenticationType, new Dictionary<string, object>()) {

        }

        public AuthenticationRequestPacket(string authenticationType, IDictionary<string, object> properties)
            : base(nameof(AuthenticationRequestPacket),
              properties ?? new Dictionary<string, object>()) {

            if (string.IsNullOrWhiteSpace(authenticationType))
                throw new ArgumentException("Can't be blank", nameof(authenticationType));

            AuthenticationType = authenticationType;
        }
    }
}
