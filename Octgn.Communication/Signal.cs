using System;
using System.Threading.Tasks;

namespace Octgn.Communication
{
    public static class Signal
    {
        public static event OnException OnException;

        public static void Exception(Exception exception, string message = null) {
            if (exception is AggregateException agg) {
                foreach (var ex in agg.InnerExceptions) {
                    Exception(ex, message);
                }
            } else {
                FireOnException(exception, message);
            }
        }

        private static async void FireOnException(Exception ex, string message) {
            var args = new ExceptionEventArgs(ex, message);

            var handler = OnException;

            if (handler == null)
                throw new InvalidOperationException($"{nameof(Signal)} caught an exception, but nothing handled it.");

            // We use await here because it will cause the async void to break off into a threadpool thread
            await Task.Factory.FromAsync(
                (callback, @object) => handler.BeginInvoke(null, (ExceptionEventArgs)@object, callback, null),
                (result) => handler.EndInvoke(result),
                args);
        }
    }

    public delegate void OnException(object sender, ExceptionEventArgs args);
    public class ExceptionEventArgs : EventArgs
    {
        public Exception Exception { get; set; }
        public string Message { get; set; }

        public ExceptionEventArgs() { }

        public ExceptionEventArgs(Exception exception, string message) {
            Exception = exception ?? throw new ArgumentNullException(nameof(exception));
            Message = message;
        }

        public override string ToString() {
            return $"{Message}{Environment.NewLine}{Exception}";
        }
    }
}
