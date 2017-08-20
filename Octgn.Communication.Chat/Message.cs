using Octgn.Communication;
using Octgn.Communication.Packets;

namespace Octgn.Communication.Chat
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

        internal override byte PacketTypeId => 10;

        public Message() : base(nameof(Message)) {

        }

        public Message(string to, string message) : base(nameof(Message)) {
            Destination = to;
            Body = message;
        }

        public override string ToString() {
            return $"Message(From: {Origin}, To: {Destination}, Body: '{Body}')";
        }
    }
}
