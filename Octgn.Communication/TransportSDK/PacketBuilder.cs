using System.Collections.Generic;
using System.Linq;

namespace Octgn.Communication.TransportSDK
{
    public class PacketBuilder
    {
        private readonly List<byte> _data = new List<byte>();
        public PacketBuilder()
        {
        }

        public PacketBuilder(PacketBuilder other) {
            _data.AddRange(other._data);
        }

        public IEnumerable<Packet> AddData(ISerializer serializer, byte[] data, int count)
        {
            _data.AddRange(data.Take(count));
            while (true) {
                var packet = Packet.Deserialize(_data, serializer, out var byteCount);

                _data.RemoveRange(0, byteCount);

                if (packet == null) break;
                yield return packet;
            }
        }
    }
}
