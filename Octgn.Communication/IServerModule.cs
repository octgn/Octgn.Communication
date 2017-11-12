using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IServerModule
    {
        Task HandleRequest(object sender, RequestReceivedEventArgs args);
        Task UserStatucChanged(object sender, UserStatusChangedEventArgs e);
    }
}
