using Octgn.Communication.Modules;
using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public static class ExtensionMethods
    {
        internal static readonly bool IsTracePacketsEnabled = false;

        public static void TracePacketReceived(this ILogger log, IConnection con, IPacket packet) {
            if (!IsTracePacketsEnabled) return;

            log.Info($"{con} <--- RECIEVED PACKET {packet} <--- {con.RemoteAddress}");
        }

        public static void TracePacketSent(this ILogger log, IConnection con, IPacket packet) {
            if (!IsTracePacketsEnabled) return;

            log.Info($"{con} ---> SENT PACKET {packet} ---> {con.RemoteAddress}");
        }

        public static void TracePacketSending(this ILogger log, IConnection con, IPacket packet) {
            if (!IsTracePacketsEnabled) return;

            log.Info($"{con} -?-> SENDING PACKET {packet} -?-> {con.RemoteAddress}");
        }

        public static void TraceWaitingForAck(this ILogger log, IConnection con, ulong packetId) {
            if (!IsTracePacketsEnabled) return;

            log.Info($"{con}: Waiting for ack for #{packetId}");
        }

        public static void TraceAckReceived(this ILogger log, IConnection con, IAck ack) {
            if (!IsTracePacketsEnabled) return;

            log.Info($"{con}: Ack for #{ack.PacketReceived} received");
        }

        public static async Task<TimeSpan> Ping(this IConnection connection) {
            var packet = new RequestPacket(nameof(PingModule.ICalls.Ping));

            var startTime = DateTime.Now;

            var result = await connection.Request(packet);

            var serverTimeReceived = result.As<DateTime>();

            return DateTime.Now - startTime;
        }

        public static Task<ResponsePacket> SendMessage(this Client client, string toUserId, string message) {
            return client.Request(new Message(toUserId, message));
        }

        public static async void SignalOnException(this Task task) {
            try {
                await task.ConfigureAwait(false);
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        public static IEnumerable<IConnection> GetConnections(this IConnectionProvider connectionProvider, string userId) {
            return connectionProvider.GetConnections().Where(con => con.User.Id.Equals(userId, StringComparison.InvariantCultureIgnoreCase));
        }

        /// <summary>
        /// Truncates a string if it's too long.
        /// Found here https://stackoverflow.com/a/6724896/222054
        /// </summary>
        /// <param name="value"></param>
        /// <param name="maxChars"></param>
        /// <returns></returns>
        public static string Truncate(this string value, int maxChars) {
            return value.Length <= maxChars ? value : value.Substring(0, maxChars) + "...";
        }
    }
}
