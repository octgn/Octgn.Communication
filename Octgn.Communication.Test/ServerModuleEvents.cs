using Octgn.Communication.Packets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication.Test
{
    public class ServerModuleEvents : Module
    {
        public event EventHandler<RequestReceivedEventArgs> Request;

        public override Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default(CancellationToken)) {
            if(obj is RequestPacket request) {
                var args = new RequestReceivedEventArgs() {
                    Request = request,
                };
                Request?.Invoke(this, args);
                return Task.FromResult(ProcessResult.Processed);
            }
            return base.Process(obj, cancellationToken);
        }

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            Request?.Invoke(sender, args);
            return Task.CompletedTask;
        }
    }
}