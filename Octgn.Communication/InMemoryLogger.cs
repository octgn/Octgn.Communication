using System;
using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace Octgn.Communication
{
    public class InMemoryLogger : ILogger
    {
        internal static System.Collections.Concurrent.ConcurrentQueue<string> LogMessages { get; set; } = new System.Collections.Concurrent.ConcurrentQueue<string>();

        public int MaxBufferSize { get; set; }

        public string Name { get; }

        public InMemoryLogger(LoggerFactory.Context context) {
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

        private void Write(string message) {
            if(Debugger.IsAttached)
                Debug.WriteLine(message);
            LogMessages.Enqueue(message);
            if (MaxBufferSize > 0) {
                if (LogMessages.Count > MaxBufferSize) {
                    LogMessages.TryDequeue(out string blah);
                }
            }
        }
    }
}
