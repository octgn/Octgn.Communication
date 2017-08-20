using System;
using System.IO;

namespace Octgn.Communication.Packets
{
    public sealed class Ack : Packet
    {
        internal override byte PacketTypeId => 255;

        public ulong PacketId { get; set; }
        public DateTimeOffset PacketReceived { get; set; }

        protected override string PacketStringData => "ACK";

        public Ack() { }

        public Ack(Packet packet)
        {
            PacketId = packet?.Id ?? throw new ArgumentException("packet or packet.Id can't be null", nameof(packet));
            PacketReceived = DateTimeOffset.Now;
        }

        internal override void Serialize(BinaryWriter writer, ISerializer serializer)
        {
            writer.Write(PacketId);
            writer.Write(PacketReceived.ToString("o"));
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer)
        {
            PacketId = reader.ReadUInt64();
            PacketReceived = DateTimeOffset.Parse(reader.ReadString());
        }
    }
}
