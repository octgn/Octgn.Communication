using Octgn.Communication.Modules;
using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public static class ExtensionMethods
    {
        public static void TracePacketReceived(this ILogger log, IConnection con, Packet packet)
        {
#if (!TRACE_PACKETS)
            return;
#endif
#pragma warning disable CS0162 // Unreachable code detected
            log.Info($"{con.ConnectionId}: <<< PACKET {packet}");
#pragma warning restore CS0162 // Unreachable code detected
        }

        public static void TracePacketSent(this ILogger log, IConnection con, Packet packet)
        {
#if (!TRACE_PACKETS)
            return;
#endif
#pragma warning disable CS0162 // Unreachable code detected
            log.Info($"{con.ConnectionId}: >>> PACKET {packet}");
#pragma warning restore CS0162 // Unreachable code detected
        }

        public static async Task<TimeSpan> Ping(this IConnection connection) {
            var packet = new RequestPacket(nameof(PingModule.ICalls.Ping));

            var startTime = DateTime.Now;

            var result = await connection.Request(packet);

            var serverTimeReceived = result.As<DateTime>();

            return DateTime.Now - startTime;
        }

        //public static Task<ResponsePacket> SendMessage(this Client client, string to, string message) {
        //    return client.Request(new Message(to, message));
        //}

        //public static Task<IDeliverable> SendMessage(this Client client, User to, string message) {
        //    return SendMessage(client, to.NodeId, message);
        //}
    }
}
