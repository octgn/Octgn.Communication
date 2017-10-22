using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Octgn.Communication.Packets;

namespace Octgn.Communication
{
    public sealed class RequestHandler
    {
        private readonly Dictionary<string,Func<RequestContext, RequestPacket,Task<ResponsePacket>>> _routes;

        public RequestHandler() {
            _routes = new Dictionary<string, Func<RequestContext, RequestPacket, Task<ResponsePacket>>>();
        }

        public void Register(string requestName, Func<RequestContext, RequestPacket, Task<ResponsePacket>> call) {
            _routes.Add(requestName, call);
        }

        public async Task HandleRequest(object sender, HandleRequestEventArgs args) {
            var packet = args.Packet;

            var context = new RequestContext {
                Connection = args.Connection,
                Server = sender as Server,
                Client = sender as Client
            };
            if(context.Server != null) {
                context.UserId = context.Server.ConnectionProvider.GetUserId(args.Connection);
            }

            if (context.Server == null && context.Client == null)
                throw new ArgumentException($"Sender must be type of {nameof(Server)} or {nameof(Client)}", nameof(sender));

            if (_routes.TryGetValue(packet.Name, out var call)) {
                args.Response = await call(context, packet);
                args.IsHandled = true;
            }
        }
    }
}
