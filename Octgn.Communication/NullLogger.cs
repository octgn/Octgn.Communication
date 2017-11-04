using System;

namespace Octgn.Communication
{
    public class NullLogger : ILogger
    {
        public string Name { get; }

        public NullLogger(LoggerFactory.Context context) {
            Name = context.Name;
        }

        public void Info(string message) {
        }

        public void Warn(string message) {
        }

        public void Warn(string message, Exception ex) {
        }

        public void Warn(Exception ex) {
        }

        public void Error(string message) {
        }

        public void Error(string message, Exception ex) {
        }

        public void Error(Exception ex) {
        }
    }
}
