using Octgn.Communication.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnection : IEquatable<IConnection>, IDisposable
    {
        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
#pragma warning disable RCS1159 // Use EventHandler<T>.
        event RequestReceived RequestReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.
#pragma warning disable RCS1159 // Use EventHandler<T>.
        event PacketReceived PacketReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.
        string ConnectionId { get; }
        string RemoteAddress { get; }
        ConnectionState State { get; }
        User User { get; }
        Task Connect(CancellationToken cancellationToken = default(CancellationToken));
        Task<ResponsePacket> Request(RequestPacket packet, CancellationToken cancellationToken = default(CancellationToken));
        Task Respond(ulong requestPacketId, ResponsePacket response, CancellationToken cancellationToken = default(CancellationToken));
        Task<IAck> Send(IPacket packet, CancellationToken cancellationToken = default(CancellationToken));
        /// <summary>
        /// Creates a new <see cref="IConnection"/> with the same <see cref="RemoteAddress"/>.
        /// and a <see cref="State"/> of <see cref="ConnectionState.Created"/>.
        /// Any unprocessed data should be copied over to the new connection.
        /// </summary>
        /// <returns></returns>
        IConnection Clone();
        /// <summary>
        /// Transitions this <see cref="IConnection"/> into the <see cref="ConnectionState.Closed"/> <see cref="State"/>
        /// </summary>
        void Close();
    }

    public enum ConnectionState
    {
        Created = 0,
        Connecting = 1,
        Handshaking = 2,
        Connected = 3,
        Closed = 4
    }

    public class ConnectionStateChangedEventArgs : EventArgs
    {
        public IConnection Connection { get; set; }
        public Exception Exception { get; set; }
        public ConnectionState NewState { get; set; }
        public ConnectionState OldState { get; set; }

        public ConnectionStateChangedEventArgs() {

        }
    }

    public delegate Task<object> RequestReceived(object sender, RequestReceivedEventArgs args);

    public class RequestReceivedEventArgs : EventArgs
    {
        public RequestContext Context { get; set; }
        public bool IsHandled { get; set; }
        public RequestPacket Request { get; set; }
        public object Response { get; set; }
    }

    public delegate Task PacketReceived(object sender, PacketReceivedEventArgs args);

    public class PacketReceivedEventArgs : EventArgs
    {
        public bool IsHandled { get; set; }

        public IConnection Connection { get; set; }
        
        public SerializedPacket Packet { get; set; }

        public ulong PacketId { get; set; }
    }
}
