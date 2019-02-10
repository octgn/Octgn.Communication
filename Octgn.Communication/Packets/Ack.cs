using System;
using System.IO;

namespace Octgn.Communication.Packets
{
    public sealed class Ack : Packet, IAck
    {
        public override byte PacketType => 255;
        public override PacketFlag Flags => PacketFlag.None;

        public ulong PacketId { get; set; }
        public DateTimeOffset PacketReceived { get; set; }

        protected override string PacketStringData => "ACK";

        public Ack()
        {
            PacketReceived = DateTimeOffset.Now;
        }

        public Ack(ulong packetId) {

            PacketId = packetId;
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
