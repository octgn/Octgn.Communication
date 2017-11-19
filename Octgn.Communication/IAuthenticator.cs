using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IAuthenticator
    {
        Task<AuthenticationResult> Authenticate(Client client, IConnection connection, int waitTimeInMs = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken));
    }
}
