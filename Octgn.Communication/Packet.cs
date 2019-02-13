using System;
using System.Collections.Generic;
using System.IO;

namespace Octgn.Communication
{
    public abstract class Packet : IPacket
    {
        public abstract byte PacketType { get; }
        public abstract PacketFlag Flags { get; }
        public DateTimeOffset Sent { get; set; }
        public string Destination { get; set; }
        public User Origin { get; internal set; }

        protected Packet()
        {

        }

        internal virtual void Serialize(BinaryWriter writer, ISerializer serializer)
        {
        }

        internal virtual void Deserialize(BinaryReader reader, ISerializer serializer)
        {
        }

        static Packet()
        {
            RegisterPacketType<Octgn.Communication.Packets.Ack>();
            RegisterPacketType<Octgn.Communication.Packets.RequestPacket>();
            RegisterPacketType<Octgn.Communication.Packets.ResponsePacket>();
            RegisterPacketType<Octgn.Communication.Packets.HandshakeRequestPacket>();
        }

        public static void RegisterPacketType<T>() where T : Packet
        {
            var inst = System.Activator.CreateInstance<T>();

            if (_packetTypes.ContainsKey(inst.PacketType))
                throw new InvalidOperationException($"Packet type {inst.PacketType} is already registered to {_packetTypes[inst.PacketType].FullName}");

            _packetTypes[inst.PacketType] = typeof(T);
        }

        public static void UnregisterPacketType<T>() where T : Packet {
            // If this method gets exposed publicly, we need to add checks in here
            var inst = System.Activator.CreateInstance<T>();

            _packetTypes.Remove(inst.PacketType);
        }

        public static bool IsPacketTypeRegistered(byte typeId) {
            return _packetTypes.ContainsKey(typeId);
        }

        private static Dictionary<byte, Type> _packetTypes = new Dictionary<byte, Type>();

        public static Packet Create(byte type)
        {
            if (_packetTypes.TryGetValue(type, out Type ptype)) {
                return (Packet)Activator.CreateInstance(ptype);
            } else throw new UnregisteredPacketException(type);
        }

        public static Type GetType(byte type) {
            _packetTypes.TryGetValue(type, out var result);

            return result;
        }

        protected abstract string PacketStringData { get; }

        private string _toString;
        public override string ToString() {
            return _toString ?? (_toString = $"#{PacketStringData}");
        }
    }
}
