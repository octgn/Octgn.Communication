using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnectionProvider : IDisposable
    {
        void Initialize(Server server);

        /// <summary>
        /// Gets all added <see cref="IConnection"/>'s. Further filtering is required to find Connected connections.
        /// </summary>
        /// <returns></returns>
        IEnumerable<IConnection> GetConnections();

        Task AddConnection(IConnection connection);

        event EventHandler<ConnectionStateChangedEventArgs> ConnectionStateChanged;
    }
}
