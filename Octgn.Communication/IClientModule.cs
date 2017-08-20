using System.Threading.Tasks;

namespace Octgn.Communication
{

    public interface IClientModule
    {
        Task HandleRequest(object sender, HandleRequestEventArgs args);
    }
}
