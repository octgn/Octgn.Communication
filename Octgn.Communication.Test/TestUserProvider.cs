using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Octgn.Communication.Test
{
    public class TestUserProvider : IConnectionProvider
    {
        public const string OnlineStatus = nameof(OnlineStatus);
        public const string OfflineStatus = nameof(OfflineStatus);
        private UserConnectionMap OnlineUsers { get; } = new UserConnectionMap();

        public IEnumerable<IConnection> GetConnections(string userId) {
            return OnlineUsers.GetConnections(userId);
        }

        public Task AddConnection(IConnection connection, string userId) {
            return OnlineUsers.AddConnection(connection, userId);
        }

        private Server _server;
        public void Initialize(Server server) {
            _server = server;
        }

        public string GetUserId(IConnection connection) {
            return OnlineUsers.GetUserId(connection);
        }

        public string GetUserStatus(string userId) {
            return OnlineUsers.GetConnections(userId).Any() ? OnlineStatus : OfflineStatus;
        }

        public IEnumerable<IConnection> GetConnections() {
            return OnlineUsers.GetConnections();
        }
    }
}
