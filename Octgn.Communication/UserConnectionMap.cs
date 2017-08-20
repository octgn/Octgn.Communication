using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;

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

        private readonly Dictionary<IConnection, User> _connectionsToUsers = new Dictionary<IConnection, User>();
        private readonly AsyncReaderWriterLock _dataLock = new AsyncReaderWriterLock();

        public async Task AddConnection(IConnection connection, User user) {
            using (var locker = await _dataLock.UpgradeableReaderLockAsync()) {
                if (_connectionsToUsers.ContainsKey(connection)) throw new InvalidOperationException($"{user} already mapped to {connection}");

                using (await locker.UpgradeAsync()) {
                    _connectionsToUsers.Add(connection, user);
                }

                connection.ConnectionClosed += UserConnection_ConnectionClosed;
                Log.Info($"Mapped {user} to {connection}");

                await FireUserConnectionChanged(user, true);
            }
        }

        private async void UserConnection_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            if (_isDisposed) return;
            using (var locker = await _dataLock.UpgradeableReaderLockAsync()) {
                try {
                    var connection = args.Connection;

                    connection.ConnectionClosed -= UserConnection_ConnectionClosed;

                    if (!_connectionsToUsers.TryGetValue(connection, out User user)) throw new InvalidOperationException($"No mapping found for {connection}");


                    using (await locker.UpgradeAsync()) {
                        _connectionsToUsers.Remove(connection);
                        Log.Info($"Removed mapping from {user} to {connection}");
                    }

                    if (!_connectionsToUsers.Any(x => x.Value.Equals(user))) {
                        // No connections left for the user, so they disconnected
                        await FireUserConnectionChanged(user, false);
                    }

                } catch (Exception ex) {
                    Log.Error(ex);
                    Signal.Exception(ex);
                }
            }
        }


        public User ValidateConnection(IConnection connection) {
            using (_dataLock.ReaderLock()) { 
                if (!_connectionsToUsers.TryGetValue(connection, out User ret))
                    throw new UnauthorizedAccessException();

                return ret;
            }
        }

        public IEnumerable<IConnection> GetConnections(string username) {
            using (_dataLock.ReaderLock()) { 
                return _connectionsToUsers
                    .Where(x => username.Equals(x.Value.NodeId, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Key)
                    .ToArray();
            }
        }

        public IEnumerable<User> GetOnlineUsers() {
            using (_dataLock.ReaderLock()) { 
                return _connectionsToUsers
                    .Select(x => x.Value)
                    .ToArray();
            }
        }

        private bool _isDisposed;
        public void Dispose() {
            if (_isDisposed)
                return;
            _isDisposed = true;

            _connectionsToUsers.Clear();
        }
    }
}
