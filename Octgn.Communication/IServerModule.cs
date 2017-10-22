using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IServerModule
    {
        Task HandleRequest(object sender, HandleRequestEventArgs args);
        Task UserStatucChanged(object sender, UserStatusChangedEventArgs e);
    }
}
