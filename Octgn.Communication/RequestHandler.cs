using Octgn.Communication.Packets;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public sealed class RequestHandler : Module
    {
        private readonly Dictionary<string, HandleRequestDelegate> _routes;

        public RequestHandler() {
            _routes = new Dictionary<string, HandleRequestDelegate>();
        }

        public void Register(string requestName, HandleRequestDelegate call) {
            _routes.Add(requestName, call);
        }

        public override Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default) {
            if(obj is RequestPacket request) {
                if (_routes.TryGetValue(request.Name, out var requestHandler)) {
                    return requestHandler(request);
                }
            }
            return base.Process(obj, cancellationToken);
        }

        public delegate Task<ProcessResult> HandleRequestDelegate(RequestPacket request);
    }
}
