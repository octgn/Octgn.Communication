using System;
using System.Runtime.Serialization;

namespace Octgn.Communication
{
    [Serializable]
    internal class InvalidDataLengthException : InvalidDataException
    {
        public InvalidDataLengthException() {
        }

        public InvalidDataLengthException(string message) : base(message) {
        }

        public InvalidDataLengthException(string message, Exception innerException) : base(message, innerException) {
        }

        public InvalidDataLengthException(string message, byte[] data) : base(message, data) {
        }

        public InvalidDataLengthException(string message, byte[] data, Exception innerException) : base(message, data, innerException) {
        }

        protected InvalidDataLengthException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
    [Serializable]
    internal class InvalidDataException : Exception
    {
        public byte[] PacketData { get; set; }

        public InvalidDataException() {
        }

        public InvalidDataException(string message) : base(message) {
        }

        public InvalidDataException(string message, Exception innerException) : base(message, innerException) {
        }

        public InvalidDataException(string message, byte[] data) : base(message) {
            PacketData = data;
        }

        public InvalidDataException(string message, byte[] data, Exception innerException) : base(message, innerException) {
            PacketData = data;
        }

        protected InvalidDataException(SerializationInfo info, StreamingContext context) : base(info, context) {
        }
    }
}