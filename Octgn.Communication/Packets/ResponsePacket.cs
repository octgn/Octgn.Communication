using System;
using System.Diagnostics;
using System.IO;

namespace Octgn.Communication.Packets
{
    public sealed class ResponsePacket : DataPacket, IAck {

        private static ILogger Log = LoggerFactory.Create(nameof(ResponsePacket));

        public ulong RequestPacketId {
            get => PacketId;
            set => PacketId = value;
        }

        public ulong PacketId { get; set; }
        public DateTimeOffset PacketReceived { get; set; }

        public ResponsePacket() { }

        public ResponsePacket(object response) : base(response) {
        }

        public override byte PacketType => 4;
        public override PacketFlag Flags => PacketFlag.None;


        protected override string PacketStringData {
            get {
                var packetIDData = string.IsNullOrWhiteSpace(DataType)
                    ? "REP+UNKNOWN"
                    : $"REP+{DataType}";

                var addPacketData = $"in response to #{RequestPacketId.ToString()}";
                return packetIDData + " " + addPacketData;
            }
        }

        internal override void Serialize(BinaryWriter writer, ISerializer serializer)
        {
            base.Serialize(writer, serializer);
            writer.Write(PacketId);
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer)
        {
            base.Deserialize(reader, serializer);

            PacketId = reader.ReadUInt64();
            PacketReceived = DateTimeOffset.Now;
        }

        [DebuggerStepThrough]
        internal void Verify() {
            if (Data is ErrorResponseData err) {
                throw new ErrorResponseException(err);
            }
        }
    }
}
