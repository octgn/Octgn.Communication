using System;

namespace Octgn.Communication
{
    public interface IConnectionCreator
    {
        IHandshaker Handshaker { get; }

        IConnection Create(string host);

        void Initialize(Client client);
    }
}
