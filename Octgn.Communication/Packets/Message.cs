namespace Octgn.Communication.Packets
{
    public class Message : RequestPacket
    {
        static Message() {
            RegisterPacketType<Message>();
        }

        public string Body {
            get => (string)this[nameof(Body)];
            set => this[nameof(Body)] = value;
        }

        public override byte PacketType => 6;

        public Message() : base(nameof(Message)) {

        }

        public Message(string to, string message) : base(nameof(Message)) {
            Destination = to;
            Body = message;
        }

        protected virtual string MessageString => $"{{From: {Origin}, To: {Destination}, Body: '{Body?.Truncate(20)}'}}";

        protected override string PacketStringData => base.PacketStringData + " " + MessageString;
    }
}
