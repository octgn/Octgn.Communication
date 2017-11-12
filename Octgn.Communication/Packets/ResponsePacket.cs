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

        public ResponsePacket() : base() { }

        public ResponsePacket(RequestPacket req) : base() {
            RequestPacketId = req?.Id ?? throw new ArgumentNullException(nameof(req));
        }

        public ResponsePacket(RequestPacket req, object response) : base(response) {
            RequestPacketId = req?.Id ?? throw new ArgumentNullException(nameof(req));
        }

        public override byte PacketTypeId => 4;
        public override bool RequiresAck => false;

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
            writer.Write(PacketReceived.ToString("o"));
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer)
        {
            base.Deserialize(reader, serializer);

            PacketId = reader.ReadUInt64();
            PacketReceived = DateTimeOffset.Parse(reader.ReadString());
        }

        [DebuggerStepThrough]
        internal void Verify() {
            if (Data is ErrorResponseData err) {
                throw new ErrorResponseException(err);
            }
        }
    }
}
