using System;
using System.Threading;
using System.Threading.Tasks;
using Octgn.Communication.Packets;

namespace Octgn.Communication.Test
{
    public class TestHandshaker : IHandshaker
    {
        public string UserId { get; set; }

        public TestHandshaker(string userId) {
            UserId = userId;
        }

        public TestHandshaker() { }

        public async Task<HandshakeResult> Handshake(IConnection connection, CancellationToken cancellation = default(CancellationToken)) {
            var authRequest = new HandshakeRequestPacket("asdf") {
                ["userid"] = UserId
            };
            var result = await connection.Request(authRequest, cancellation);
            return result.As<HandshakeResult>();
        }

        public Task<HandshakeResult> OnHandshakeRequest(HandshakeRequestPacket request, IConnection connection, CancellationToken cancellation = default(CancellationToken)) {
            var userId = (string)request["userid"];

            return Task.FromResult(HandshakeResult.Success(new User(userId, userId)));
        }
    }
}
