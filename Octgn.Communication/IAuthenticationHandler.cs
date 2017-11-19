using Octgn.Communication.Packets;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IAuthenticationHandler
    {
        Task<AuthenticationResult> Authenticate(Server server, IConnection connection, AuthenticationRequestPacket packet, int waitTimeInMs = Timeout.Infinite, CancellationToken cancellationToken = default(CancellationToken));
    }
}