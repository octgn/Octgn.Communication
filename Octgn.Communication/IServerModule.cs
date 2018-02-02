using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IServerModule
    {
        Task HandleRequest(object sender, RequestReceivedEventArgs args);
        Task UserStatusChanged(object sender, UserStatusChangedEventArgs e);
    }
}
