using System;

namespace Octgn.Communication
{
    public class CouldNotConnectException : Exception
    {
        public CouldNotConnectException() : base() {
        }

        public CouldNotConnectException(string message) : base(message) {
        }

        public CouldNotConnectException(string message, Exception innerException) : base(message, innerException) {
        }
    }
}
