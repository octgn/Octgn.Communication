using System;

namespace Octgn.Communication
{
    public interface IConnectionCreator
    {
        public IHandshaker Handshaker { get; }

        IConnection Create(string host);

        void Initialize(Client client);
    }
}
