using System;
using System.Threading.Tasks;
using Octgn.Communication.Messages;
using System.Collections.Generic;

namespace Octgn.Communication.Test
{
    public class TestUserProvider : IUserProvider
    {
        public virtual User GetUser(string username) {
            return null;
        }

        public virtual void UpdateUser(User user) {
        }

        private UserConnectionMap OnlineUsers { get; } = new UserConnectionMap();

        public User ValidateConnection(IConnection connection) {
            return OnlineUsers.ValidateConnection(connection);
        }

        public IEnumerable<IConnection> GetConnections(string username) {
            return OnlineUsers.GetConnections(username);
        }

        public Task AddConnection(IConnection connection, User user) {
            return OnlineUsers.AddConnection(connection, user);
        }

        public virtual LoginResultType ValidateUser(string username, string password, out User user) {
            if (username.StartsWith("#")) {
                user = null;
                return (LoginResultType)Enum.Parse(typeof(LoginResultType), username.Substring(1));
            }

            user = new User(username);
            return LoginResultType.Ok;
        }

        private Server _server;
        public void Initialize(Server server) {
            _server = server;
        }
    }
}
