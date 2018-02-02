using System;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;

namespace Octgn.Communication.Test
{
    public class TestUserProvider : IConnectionProvider, IDisposable
    {
        public const string OnlineStatus = nameof(OnlineStatus);
        public const string OfflineStatus = nameof(OfflineStatus);
        private UserConnectionMap OnlineUsers { get; } = new UserConnectionMap();

        public TestUserProvider() {
            OnlineUsers.UserConnectionChanged += OnlineUsers_UserConnectionChanged;
        }

        private async void OnlineUsers_UserConnectionChanged(object sender, UserConnectionChangedEventArgs e) {
            try {
                await _server.UpdateUserStatus(e.User, e.IsConnected ? TestUserProvider.OnlineStatus : TestUserProvider.OfflineStatus);
            } catch (ObjectDisposedException) {
            } catch (Exception ex) {
                Signal.Exception(ex);
            }
        }

        public IEnumerable<IConnection> GetConnections(string userId) {
            return OnlineUsers.GetConnections(userId);
        }

        public Task AddConnection(IConnection connection, User user) {
            return OnlineUsers.AddConnection(connection, user);
        }

        private Server _server;
        public void Initialize(Server server) {
            _server = server;
        }

        public User GetUser(IConnection connection) {
            return OnlineUsers.GetUser(connection);
        }

        public string GetUserStatus(string userId) {
            return OnlineUsers.GetConnections(userId).Any() ? OnlineStatus : OfflineStatus;
        }

        public IEnumerable<IConnection> GetConnections() {
            return OnlineUsers.GetConnections();
        }

        #region IDisposable Support
        private bool _disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!_disposedValue) {
                if (disposing) {
                    OnlineUsers.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                _disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~TestUserProvider() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
