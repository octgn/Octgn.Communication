using System;

namespace Octgn.Communication
{

    public class RequestContext
    {
        /// <summary>
        /// <see cref="IConnection"/> that the <see cref="RequestPacket"/> was received on.
        /// </summary>
        public IConnection Connection { get; set; }
        /// <summary>
        /// The <see cref="Communication.User"/> who sent the <see cref="RequestPacket"/>.
        /// </summary>
        public User User => Server != null ? Connection?.User : null;
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
            } else {
                throw new InvalidOperationException($"No server of Client");
            }

            return $"{soc}: {Connection}: {User}";
        }
    }
}
