using System;

namespace Octgn.Communication
{
    public interface IConnectionCreator
    {
        IConnection Create(string host);
        void Initialize(Client client);
    }
}
