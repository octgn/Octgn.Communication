using System;
using System.Collections.Concurrent;

namespace Octgn.Communication
{
    public static class Signal
    {
        public static event OnException OnException;
        public static ConcurrentQueue<ExceptionEventArgs> Exceptions { get; } = new ConcurrentQueue<ExceptionEventArgs>();

        public static void Exception(Exception ex, string message = null) {
            var args = new ExceptionEventArgs {
                Exception = ex,
                Message = message,
            };

            if (OnException != null) {
                OnException?.BeginInvoke(null, args, null, null);
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
