using System;
using System.Diagnostics;
using System.IO;

namespace Octgn.Communication.Packets
{
    public sealed class ResponsePacket : DataPacket {

        private static ILogger Log = LoggerFactory.Create(nameof(ResponsePacket));

        public ulong InResponseTo { get; set; }

        public ResponsePacket() : base() { }

        public ResponsePacket(RequestPacket req) : base() {
            InResponseTo = req?.Id ?? throw new ArgumentNullException(nameof(req));
        }

        public ResponsePacket(RequestPacket req, object response) : base(response) {
            InResponseTo = req?.Id ?? throw new ArgumentNullException(nameof(req));
        }

        internal override byte PacketTypeId => 4;

        protected override string PacketStringData {
            get {
                var packetIDData = string.IsNullOrWhiteSpace(DataType)
                    ? "REP+UNKNOWN"
                    : $"REP+{DataType}";

                var addPacketData = $"in response to #{InResponseTo.ToString()}";
                return packetIDData + " " + addPacketData;
            }
        }

        internal override void Serialize(BinaryWriter writer, ISerializer serializer)
        {
            base.Serialize(writer, serializer);
            writer.Write(InResponseTo);
        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer)
        {
            base.Deserialize(reader, serializer);

            InResponseTo = reader.ReadUInt64();
        }

        [DebuggerStepThrough]
        internal void Verify() {
            if (Data is ErrorResponseData err) {
                throw new ErrorResponseException(err);
            }
        }
    }
}
