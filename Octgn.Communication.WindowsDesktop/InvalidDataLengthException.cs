using System;
using System.Runtime.Serialization;

namespace Octgn.Communication
{
    [Serializable]
    internal class InvalidDataLengthException : Exception
    {
        public InvalidDataLengthException() {
        }

        public InvalidDataLengthException(string message) : base(message) {
        }

        public InvalidDataLengthException(string message, Exception innerException) : base(message, innerException) {
        }

        protected InvalidDataLengthException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}