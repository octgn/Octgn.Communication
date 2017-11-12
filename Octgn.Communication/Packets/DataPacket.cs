using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;

namespace Octgn.Communication.Packets
{
    public abstract class DataPacket : Packet
    {
        private static ILogger Log => LoggerFactory.Create(typeof(DataPacket));

        public string DataType { get; private set; }

        public object Data {
            get;
            protected set;
        }

        [DebuggerStepThrough]
        public T As<T>() {
            object ret = Data;
            try {
                if (Data is long && typeof(T).GetTypeInfo().IsEnum) {
                    // Because json.net always deserializes ints to longs when it doesn't know the type name.
                    ret = Convert.ToInt32(Data);
                }
                return (T)ret;

            } catch (InvalidCastException ex) {
                throw new InvalidCastException($"Type is `{DataType}`", ex);
            }
        }

        protected DataPacket()
        {
            DataType = "NULL";
        }

        public DataPacket(object o)
        {
            DataType = (o != null) ? o.GetType().AssemblyQualifiedName : "NULL";
            Data = o;
        }

        private readonly static byte[] EMPTYARRAY = new byte[0];
        internal override void Serialize(BinaryWriter writer, ISerializer serializer)
        {
            var data = Data;

            var dataBytes = data != null
                ? serializer.Serialize(data)
                : EMPTYARRAY;

            writer.Write(data?.GetType().AssemblyQualifiedName ?? "NULL");
            writer.Write(dataBytes.Length);

            writer.Write(dataBytes);

        }

        internal override void Deserialize(BinaryReader reader, ISerializer serializer)
        {
            DataType = reader.ReadString();
            var dataLength = reader.ReadInt32();
            var hasData = dataLength > 0;
            var isNull = DataType == "NULL";

            var data = hasData
                ? reader.ReadBytes(dataLength)
                : null;

            if (isNull) return;

            // We do this even if we don't have data just to verify everything is ok
            var dataType = Type.GetType(DataType, true);

            if (hasData) {
                Data = serializer.Deserialize(dataType, data);
            }
        }
    }
}
