using Octgn.Communication.Packets;
using System.Threading;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IHandshaker
    {
        Task<HandshakeResult> Handshake(IConnection connection, CancellationToken cancellation);
        Task<HandshakeResult> OnHandshakeRequest(HandshakeRequestPacket request, IConnection connection, CancellationToken cancellation);
    }
}
