using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Octgn.Communication.Utility
{
    public class BackgroundTasks : IDisposable, IEnumerable<Task>
    {
        private readonly ConcurrentDictionary<Task, byte> _pendingTasks = new ConcurrentDictionary<Task, byte>();

        public BackgroundTasks() {
        }

        public void Schedule(Task task) {
            if (disposedValue) throw new ObjectDisposedException(nameof(BackgroundTasks));

            _pendingTasks.TryAdd(task, 0);
            task.ContinueWith(t => {
                _pendingTasks.TryRemove(t, out byte _);
            });
        }

        public IEnumerator<Task> GetEnumerator() {
            return _pendingTasks.Keys.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator() {
            return _pendingTasks.Keys.GetEnumerator();
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing) {
            if (!disposedValue) {
                disposedValue = true;

                if (disposing) {
                    Task.WhenAll(_pendingTasks.Keys).Wait();
                }
            }
        }

        // This code added to correctly implement the disposable pattern.
        public void Dispose() {
           // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
        }
        #endregion
    }
}
