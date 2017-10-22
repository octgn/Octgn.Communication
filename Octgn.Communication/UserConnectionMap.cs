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

        private readonly Dictionary<IConnection, string> _connectionsToUsers = new Dictionary<IConnection, string>();
        private readonly AsyncReaderWriterLock _dataLock = new AsyncReaderWriterLock();

        public async Task AddConnection(IConnection connection, string userId) {
            using (var locker = await _dataLock.UpgradeableReaderLockAsync()) {
                if (_connectionsToUsers.ContainsKey(connection)) throw new InvalidOperationException($"{userId} already mapped to {connection}");

                using (await locker.UpgradeAsync()) {
                    _connectionsToUsers.Add(connection, userId);
                }

                connection.ConnectionClosed += UserConnection_ConnectionClosed;
                Log.Info($"Mapped {userId} to {connection}");

                await FireUserConnectionChanged(userId, true);
            }
        }

        private async void UserConnection_ConnectionClosed(object sender, ConnectionClosedEventArgs args) {
            if (_isDisposed) return;
            using (var locker = await _dataLock.UpgradeableReaderLockAsync()) {
                try {
                    var connection = args.Connection;

                    connection.ConnectionClosed -= UserConnection_ConnectionClosed;

                    if (!_connectionsToUsers.TryGetValue(connection, out string userId)) throw new InvalidOperationException($"No mapping found for {connection}");


                    using (await locker.UpgradeAsync()) {
                        _connectionsToUsers.Remove(connection);
                        Log.Info($"Removed mapping from {userId} to {connection}");
                    }

                    if (!_connectionsToUsers.Any(x => x.Value.Equals(userId))) {
                        // No connections left for the user, so they disconnected
                        await FireUserConnectionChanged(userId, false);
                    }

                } catch (Exception ex) {
                    Log.Error(ex);
                    Signal.Exception(ex);
                }
            }
        }

        public string GetUserId(IConnection connection) {
            using (_dataLock.ReaderLock()) {
                _connectionsToUsers.TryGetValue(connection, out string ret);
                return ret;
            }
        }

        public IEnumerable<IConnection> GetConnections(string username) {
            using (_dataLock.ReaderLock()) { 
                return _connectionsToUsers
                    .Where(x => username.Equals(x.Value, StringComparison.OrdinalIgnoreCase))
                    .Select(x => x.Key)
                    .ToArray();
            }
        }

        public IEnumerable<string> GetOnlineUsers() {
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
