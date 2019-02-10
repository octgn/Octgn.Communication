using System;

namespace Octgn.Communication
{
    public class UnregisteredPacketException : Exception
    {
        public UnregisteredPacketException() : base() {
        }

        public UnregisteredPacketException(IPacket packet) : base($"Packet {packet} is not registered for serialization.") {

        }

        public UnregisteredPacketException(byte packetTypeId) : base($"Packet with type id {packetTypeId} is not registered for serialization.") {

        }

        public UnregisteredPacketException(string message) : base(message) {
        }

        public UnregisteredPacketException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
