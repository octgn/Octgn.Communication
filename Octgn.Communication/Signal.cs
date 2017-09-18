using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public static class Signal
    {
        public static event OnException OnException;

        public static ConcurrentQueue<ExceptionEventArgs> Exceptions { get; } = new ConcurrentQueue<ExceptionEventArgs>();

        public static void Exception(Exception ex, string message = null) {
            FireOrQueueException(ex, message);

        }

        private static async void FireOrQueueException(Exception ex, string message) {
            var args = new ExceptionEventArgs {
                Exception = ex,
                Message = message,
            };

            if (OnException != null) {
                try {
                    await Task.Run(() => { // Run on a threadpool thread
                        OnException?.Invoke(null, args);
                    });
                } catch (Exception innerException) {
                    Exceptions.Enqueue(args);
                    Exceptions.Enqueue(new ExceptionEventArgs {
                        Exception = innerException,
                        Message = nameof(FireOrQueueException)
                    });
                }
            } else {
                Exceptions.Enqueue(args);
            }
        }
    }

    public delegate void OnException(object sender, ExceptionEventArgs args);
    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Message { get; set; }

        public override string ToString() {
            return Message + Environment.NewLine + Exception.ToString();
        }
    }
}
