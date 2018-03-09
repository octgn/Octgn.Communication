using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;
using System.Threading;

namespace Octgn.Communication.Modules
{
    public class PingModule : Module {
        public PingModule() {
        }

        public override async Task<ProcessResult> Process(object obj, CancellationToken cancellationToken = default) {
            if(obj is RequestPacket request) {
                switch (request.Name) {
                    case nameof(ICalls.Ping): {
                            return new ProcessResult(DateTime.UtcNow);
                        }
                }
            }

            return await base.Process(obj, cancellationToken).ConfigureAwait(false);
        }

        public interface ICalls
        {
            Task Ping();
        }
    }
}