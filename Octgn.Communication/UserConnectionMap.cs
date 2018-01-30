using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Collections.Concurrent;

namespace Octgn.Communication
{
    public class UserConnectionMap : IDisposable
    {
        private static ILogger Log = LoggerFactory.Create(nameof(UserConnectionMap));

        public event EventHandler<UserConnectionChangedEventArgs> UserConnectionChanged;
        protected async Task FireUserConnectionChanged(User user, bool isConnected) {
            var eve = UserConnectionChanged;
            if (eve != null) {
                var args = new UserConnectionChangedEventArgs {
                    User = user,
                    IsConnected = isConnected
                };

                await Task.Factory.FromAsync((asyncCallback, @object) =>
                    eve.BeginInvoke(this, args, asyncCallback, @object),
                    eve.EndInvoke, null);
            }
        }

        private readonly ConcurrentDictionary<IConnection, User> _connectionToUsers = new ConcurrentDictionary<IConnection, User>();

        public async Task AddConnection(IConnection connection, User user) {
            if (connection == null) throw new ArgumentNullException(nameof(connection));
            if (user == null) throw new ArgumentNullException(nameof(user));
            if (string.IsNullOrWhiteSpace(user.Id)) throw new InvalidOperationException($"User {user} is invalid: {nameof(User.Id)}");
            if (string.IsNullOrWhiteSpace(user.DisplayName)) throw new InvalidOperationException($"User {user} is invalid: {nameof(User.DisplayName)}");

            if(!_connectionToUsers.TryAdd(connection, user))
               throw new InvalidOperationException($"{user} already mapped to {connection}");

            connection.ConnectionClosed += UserConnection_ConnectionClosed;
            Log.Info($"Mapped {user} to {connection}");

            await FireUserConnectionChanged(user, true);
        }

        private async void UserConnection_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            if (_isDisposed) return;
            try {
                var connection = args.Connection;

                connection.ConnectionClosed -= UserConnection_ConnectionClosed;

                if(!_connectionToUsers.TryRemove(connection,out User user))
                    throw new InvalidOperationException($"No mapping found for {connection}");

                Log.Info($"Removed mapping from {user} to {connection}");

                if (!_connectionToUsers.Any(x => x.Value.Equals(user))) {
                    // No connections left for the user, so they disconnected
                    await FireUserConnectionChanged(user, false);
                }

            } catch (Exception ex) {
                Log.Error(ex);
                Signal.Exception(ex);
            }
        }

        public User GetUser(IConnection connection) {
            _connectionToUsers.TryGetValue(connection, out User user);
            return user;
        }

        public IEnumerable<IConnection> GetConnections() {
            return _connectionToUsers.Select(x => x.Key).ToArray();
        }

        public IEnumerable<IConnection> GetConnections(string userId) {
            if (userId == null) throw new ArgumentNullException(nameof(userId));

            return _connectionToUsers
                .Where(x => userId.Equals(x.Value.Id, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key)
                .ToArray();
        }

        public IEnumerable<User> GetOnlineUsers() {
            return _connectionToUsers
                .Select(x => x.Value)
                .ToArray();
        }

        private bool _isDisposed;
        public void Dispose() {
            if (_isDisposed)
                return;
            _isDisposed = true;

            _connectionToUsers.Clear();
        }
    }
}
