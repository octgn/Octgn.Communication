using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Octgn.Communication
{
    public abstract class Packet
    {
        internal abstract byte PacketTypeId { get; }

        public ulong? Id { get; internal set; }
        public DateTimeOffset Sent { get; set; }
        public string Destination { get; set; }
        public string Origin { get; internal set; }

        protected Packet()
        {

        }

        internal virtual void Serialize(BinaryWriter writer, ISerializer serializer)
        {
        }

        internal virtual void Deserialize(BinaryReader reader, ISerializer serializer)
        {
        }

        public static byte[] Serialize(Packet packet, ISerializer serializer)
        {
            // Result:
            // PacketLength - int
            // PacketType - byte
            // PacketId - ulong
            // PacketDestination - string
            // PacketOrigin - string
            // PacketSent - string(datetimeoffset "o" format)
            // Additional Packet Data == APD
            // APDLength - int
            // APDBytes - byte[]
            if (packet == null) throw new ArgumentNullException(nameof(packet));
            if (packet.Id == null) throw new ArgumentException("Packet must have a valid id", nameof(packet));
            if (!_packetTypes.ContainsKey(packet.PacketTypeId)) throw new UnregisteredPacketException(packet);
            using (var ms = new MemoryStream())
            using (var writer = new BinaryWriter(ms)) {

                // Serialize the packet data so we can get it's length
                packet.Serialize(writer, serializer);
                writer.Flush();

                // Get the packet data length
                var packetData = ms.ToArray();
                int packetDataLength = packetData.Length;

                { // Reset the stream
                    ms.Position = 0;
                    ms.SetLength(0);
                }

                // Write the header information
                writer.Write(packet.PacketTypeId);
                writer.Write(packet.Id.Value);
                writer.Write(packet.Destination ?? string.Empty);
                writer.Write(packet.Origin ?? string.Empty);
                writer.Write(packet.Sent.ToString("o"));

                // Write the packet data
                writer.Write(packetDataLength);
                writer.Write(packetData);

                writer.Flush();

                var entirePacketData = ms.ToArray();

                var entirePacketLengthData = BitConverter.GetBytes(entirePacketData.Length);

                Array.Resize(ref entirePacketLengthData, entirePacketLengthData.Length + entirePacketData.Length);
                entirePacketData.CopyTo(entirePacketLengthData, sizeof(int));

                return entirePacketLengthData;
            }
        }

        /// <summary>
        /// Returns a packet if it works.
        /// If there's not enough data it'll return null.
        /// If the data is invalid somehow it'll throw an exception
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        public static Packet Deserialize(List<byte> data, ISerializer serializer, out int bytesConsumed)
        {
            bytesConsumed = 0;

            var dataLength = data.Count;

            // Size of an long that tells us how long our packet is
            if (dataLength < sizeof(int)) return null;

            var packetLength = BitConverter.ToInt32(data.Take(sizeof(int)).ToArray(), 0);

            // Total length of data we need in order to deserialize the packet
            var requiredBytes = packetLength + sizeof(int);

            if (dataLength < requiredBytes) return null;

            using (var ms = new MemoryStream(data.Take(requiredBytes).ToArray()))
            using (var reader = new BinaryReader(ms)) {
                var readerLength = reader.ReadInt32();
                if (packetLength != readerLength) throw new CorruptDataException("Lengths do not match");

                var packetTypeId = reader.ReadByte();
                var packetId = reader.ReadUInt64();
                var packetDestination = reader.ReadString();
                var packetOrigin = reader.ReadString();
                string tstr = null;
                var packetSent = DateTimeOffset.Parse(tstr = reader.ReadString());

                var packetDataLength = reader.ReadInt32();

                var newPacket = Create(packetTypeId);
                newPacket.Id = packetId;
                newPacket.Sent = packetSent;
                newPacket.Destination = packetDestination;
                newPacket.Origin = packetOrigin;

                newPacket.Deserialize(reader, serializer);

                bytesConsumed = requiredBytes;
                return newPacket;
            }

        }

        static Packet()
        {
            RegisterPacketType<Octgn.Communication.Packets.Ack>();
            RegisterPacketType<Octgn.Communication.Packets.RequestPacket>();
            RegisterPacketType<Octgn.Communication.Packets.ResponsePacket>();
            RegisterPacketType<Octgn.Communication.Packets.AuthenticationRequestPacket>();
        }

        public static void RegisterPacketType<T>() where T : Packet
        {
            var inst = System.Activator.CreateInstance<T>();

            if (_packetTypes.ContainsKey(inst.PacketTypeId))
                throw new InvalidOperationException($"Packet type {inst.PacketTypeId} is already registered to {_packetTypes[inst.PacketTypeId].FullName}");

            _packetTypes[inst.PacketTypeId] = typeof(T);
        }

        internal static void UnregisterPacketType<T>() where T : Packet {
            // If this method gets exposed publically, we need to add checks in here
            var inst = System.Activator.CreateInstance<T>();

            _packetTypes.Remove(inst.PacketTypeId);
        }

        private static Dictionary<byte, Type> _packetTypes = new Dictionary<byte, Type>();

        private static Packet Create(byte type)
        {
            if (_packetTypes.TryGetValue(type, out Type ptype)) {
                return (Packet)Activator.CreateInstance(ptype);
            } else throw new UnregisteredPacketException(type);
        }

        protected abstract string PacketStringData { get; }

        private string _toString;
        public override string ToString() {
            return _toString ?? (_toString = $"#{Id?.ToString()}{PacketStringData}");
        }
    }

    public class UnregisteredPacketException : Exception
    {
        public UnregisteredPacketException() : base() {
        }

        public UnregisteredPacketException(Packet packet) : base($"Packet {packet} is not registered for serialization.") {

        }

        public UnregisteredPacketException(byte packetTypeId) : base($"Packet with type id {packetTypeId} is not registered for serialization.") {

        }

        public UnregisteredPacketException(string message) : base(message) {
        }

        public UnregisteredPacketException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
