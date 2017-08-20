using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IServerModule
    {
        Task HandleRequest(object sender, HandleRequestEventArgs args);
        Task UserChanged(object sender, UserChangedEventArgs e);
    }
}
