using System;

namespace Octgn.Communication
{
    public class DisconnectedException : Exception
    {
        public DisconnectedException() {

        }

        public DisconnectedException(string message) : base(message) {

        }

        public DisconnectedException(string mesage, Exception innerException) : base(mesage, innerException) {

        }

        public DisconnectedException(Exception innerException) : base("Disconnected", innerException) {

        }
    }
}
