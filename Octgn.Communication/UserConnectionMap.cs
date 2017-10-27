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
        protected async Task FireUserConnectionChanged(string userId, bool isConnected) {
            var eve = UserConnectionChanged;
            if (eve != null) {
                var args = new UserConnectionChangedEventArgs {
                    UserId = userId,
                    IsConnected = isConnected
                };

                await Task.Factory.FromAsync((asyncCallback, @object) =>
                    eve.BeginInvoke(this, args, asyncCallback, @object),
                    eve.EndInvoke, null);
            }
        }

        private readonly ConcurrentDictionary<IConnection, string> _connectionToUsers = new ConcurrentDictionary<IConnection, string>();

        public async Task AddConnection(IConnection connection, string userId) {
            if(!_connectionToUsers.TryAdd(connection, userId))
               throw new InvalidOperationException($"{userId} already mapped to {connection}");

            connection.ConnectionClosed += UserConnection_ConnectionClosed;
            Log.Info($"Mapped {userId} to {connection}");

            await FireUserConnectionChanged(userId, true);
        }

        private async void UserConnection_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            if (_isDisposed) return;
            try {
                var connection = args.Connection;

                connection.ConnectionClosed -= UserConnection_ConnectionClosed;

                if(!_connectionToUsers.TryRemove(connection,out string userId))
                    throw new InvalidOperationException($"No mapping found for {connection}");

                Log.Info($"Removed mapping from {userId} to {connection}");

                if (!_connectionToUsers.Any(x => x.Value.Equals(userId))) {
                    // No connections left for the user, so they disconnected
                    await FireUserConnectionChanged(userId, false);
                }

            } catch (Exception ex) {
                Log.Error(ex);
                Signal.Exception(ex);
            }
        }

        public string GetUserId(IConnection connection) {
            _connectionToUsers.TryGetValue(connection, out string ret);
            return ret;
        }

        public IEnumerable<IConnection> GetConnections(string username) {
            return _connectionToUsers
                .Where(x => username.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
                .Select(x => x.Key)
                .ToArray();
        }

        public IEnumerable<string> GetOnlineUsers() {
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
