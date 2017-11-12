using Octgn.Communication.Packets;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public sealed class RequestHandler
    {
        private readonly Dictionary<string, RequestReceived> _routes;

        public RequestHandler() {
            _routes = new Dictionary<string, RequestReceived>();
        }

        public void Register(string requestName, RequestReceived call) {
            _routes.Add(requestName, call);
        }

        public async Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            var packet = args.Request;

            if(args.Context.Server != null) {
                args.Context.UserId = args.Context.Server.ConnectionProvider.GetUserId(args.Context.Connection);
            }

            if (args.Context.Server == null && args.Context.Client == null)
                throw new ArgumentException($"Sender must be type of {nameof(Server)} or {nameof(Client)}", nameof(sender));

            if (_routes.TryGetValue(packet.Name, out var call)) {
                var task = call(sender, args);
                await task;
                if(task is Task<ResponsePacket> responseTask) {
                    args.Response = responseTask.Result;
                    args.IsHandled = true;
                }
            }
        }
    }
}
