using System;

namespace Octgn.Communication
{
    public class HandshakeException : Exception
    {
        public string ErrorCode { get; set; }

        public HandshakeException() : base() {
        }

        public HandshakeException(string errorCode) : base(GenerateMessage(errorCode)) {
            ErrorCode = errorCode;
        }

        public HandshakeException(string errorCode, Exception innerException) : base(GenerateMessage(errorCode), innerException) {
            ErrorCode = errorCode;
        }

        private static string GenerateMessage(string errorCode) {
            return errorCode;
        }
    }
}
