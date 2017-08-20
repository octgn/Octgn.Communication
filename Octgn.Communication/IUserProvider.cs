using Octgn.Communication.Messages;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public interface IUserProvider
    {
        void Initialize(Server server);

        LoginResultType ValidateUser(string username, string password, out User user);

        void UpdateUser(User user);

        User GetUser(string username);

        User ValidateConnection(IConnection connection);

        IEnumerable<IConnection> GetConnections(string username);

        Task AddConnection(IConnection connection, User user);
    }
}
