using System;

namespace Octgn.Communication
{

    public class NotConnectedException : Exception
    {
        public NotConnectedException() {

        }

        public NotConnectedException(string message) : base(message) {

        }

        public NotConnectedException(string mesage, Exception innerException) : base(mesage, innerException) {

        }

        public NotConnectedException(Exception innerException) : base("Not Connected", innerException) {

        }
    }
}
