using Octgn.Communication.Packets;
using System;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Octgn.Communication.Modules
{
    public class PingModule : IServerModule, IClientModule {
        private readonly RequestHandler _requestHandler = new RequestHandler();

        public PingModule() {
            _requestHandler.Register(nameof(ICalls.Ping), OnPing);
        }

        public Task HandleRequest(object sender, RequestReceivedEventArgs args) {
            return _requestHandler.HandleRequest(sender, args);
        }
        private Task OnPing(object sender, RequestReceivedEventArgs args) {
            return Task.FromResult(new ResponsePacket(args.Request, DateTime.UtcNow));
        }

        public Task UserStatucChanged(object sender, UserStatusChangedEventArgs e) {
            return Task.FromResult<object>(null);
        }

        public IEnumerable<Type> IncludedTypes() {
            throw new NotImplementedException();
        }

        public interface ICalls
        {
            Task Ping();
        }
    }
}