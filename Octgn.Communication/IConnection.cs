using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnection : IEquatable<IConnection>
    {
        event EventHandler<ConnectionClosedEventArgs> ConnectionClosed;
#pragma warning disable RCS1159 // Use EventHandler<T>.
        event RequestReceived RequestReceived;
#pragma warning restore RCS1159 // Use EventHandler<T>.
        string ConnectionId { get; }
        bool IsClosed { get; set; }
        ISerializer Serializer { get; set; }
        Task Connect();
        Task<ResponsePacket> Request(RequestPacket packet);
        IConnection Clone();
    }

    public class ConnectionClosedEventArgs : EventArgs
    {
        public IConnection Connection { get; set; }
        public Exception Exception { get; set; }

        public ConnectionClosedEventArgs() {

        }
    }

    public delegate Task RequestReceived(object sender, RequestReceivedEventArgs args);

    public class RequestReceivedEventArgs : EventArgs
    {
        public RequestContext Context { get; set; }
        public bool IsHandled { get; set; }
        public RequestPacket Request { get; set; }
        public ResponsePacket Response { get; set; }
    }

    public class RequestContext
    {
        /// <summary>
        /// <see cref="IConnection"/> that the <see cref="RequestPacket"/> was received on.
        /// </summary>
        public IConnection Connection { get; set; }
        /// <summary>
        /// The <see cref="Communication.User"/> who sent the <see cref="RequestPacket"/>.
        /// </summary>
        public User User { get; set; }
        /// <summary>
        /// The <see cref="Server"/> that received the <see cref="RequestPacket"/>.
        /// </summary>
        public Server Server { get; set; }
        /// <summary>
        /// The <see cref="Client"/> that received the <see cref="RequestPacket"/>.
        /// </summary>
        public Client Client { get; set; }

        public override string ToString() {
            var soc = "unknown";

            if (Server != null) {
                soc = Server.ToString();
            } else if (Client != null) {
                soc = Client.ToString();
            } else throw new InvalidOperationException($"No server of Client");

            return $"{soc}: {Connection}: {User}";
        }
    }
}
