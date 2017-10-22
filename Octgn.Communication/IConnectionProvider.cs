using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnectionProvider
    {
        void Initialize(Server server);

        IEnumerable<IConnection> GetConnections(string userId);
        string GetUserId(IConnection connection);
        string GetUserStatus(string userId);

        Task AddConnection(IConnection connection, string userId);
    }
}
