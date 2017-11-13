using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IConnectionProvider
    {
        void Initialize(Server server);

        IEnumerable<IConnection> GetConnections();
        IEnumerable<IConnection> GetConnections(string userId);
        User GetUser(IConnection connection);
        string GetUserStatus(string userId);

        Task AddConnection(IConnection connection, User user);
    }
}
