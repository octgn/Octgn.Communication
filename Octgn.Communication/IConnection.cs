using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnection : IEquatable<IConnection>
    {
        event ConnectionClosed ConnectionClosed;
        event RequestPacketReceived RequestReceived;
        string ConnectionId { get; }
        bool IsClosed { get; set; }
        ISerializer Serializer { get; set; }
        Task Connect();
        Task<ResponsePacket> Request(RequestPacket packet);
        Task Response(ResponsePacket packet);
        IConnection Clone();
    }

    public delegate void ConnectionClosed(object sender, ConnectionClosedEventArgs args);
    public class ConnectionClosedEventArgs : EventArgs
    {
        public IConnection Connection { get; set; }
        public Exception Exception { get; set; }

        public ConnectionClosedEventArgs() {

        }
    }

    public delegate void RequestPacketReceived(object sender, RequestPacketReceivedEventArgs args);

    public class RequestPacketReceivedEventArgs : EventArgs
    {
        public IConnection Connection { get; set; }
        public RequestPacket Packet { get; set; }
    }
}
