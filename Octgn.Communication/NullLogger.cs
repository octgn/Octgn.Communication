using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Octgn.Communication
{
    public class NullLogger : ILogger
    {
#if(DEBUG)
        internal static System.Collections.Concurrent.ConcurrentQueue<string> LogMessages { get; set; } = new System.Collections.Concurrent.ConcurrentQueue<string>();
#endif

        public string Name { get; }

        public NullLogger(LoggerFactory.Context context) {
            Name = context.Name;
        }

        public void Info(string message) {
            Write(FormatMessage(message));
        }

        public void Warn(string message) {
            Write(FormatMessage(message));
        }

        public void Warn(string message, Exception ex) {
            Write(FormatMessage(message, ex));
        }

        public void Warn(Exception ex) {
            Write(FormatMessage("Exception", ex));
        }

        public void Error(string message) {
            Write(FormatMessage(message));
        }

        public void Error(string message, Exception ex) {
            Write(FormatMessage(message, ex));
        }

        public void Error(Exception ex) {
            Write(FormatMessage("Exception", ex));
        }

        private string FormatMessage(string message, Exception ex = null, [CallerMemberName]string caller = null) {
            var ext = ex == null ? string.Empty : Environment.NewLine + ex.ToString();
            return $"{caller.ToUpper()}: {Name}: {message} {ext}";
        }

        private static void Write(string message) {
            Debug.WriteLine(message);
#if(DEBUG)
            LogMessages.Enqueue(message);
            if(LogMessages.Count > 200) {
                LogMessages.TryDequeue(out string blah);
            }
#endif
        }
    }
}
