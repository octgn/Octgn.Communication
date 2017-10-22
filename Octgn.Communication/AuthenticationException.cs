using System;

namespace Octgn.Communication
{
    public class AuthenticationException : Exception
    {
        public string ErrorCode { get; set; }

        public AuthenticationException() : base() {
        }

        public AuthenticationException(string errorCode) : base(GenerateMessage(errorCode)) {
            ErrorCode = errorCode;
        }

        public AuthenticationException(string errorCode, Exception innerException) : base(GenerateMessage(errorCode), innerException) {
            ErrorCode = errorCode;
        }

        private static string GenerateMessage(string errorCode) {
            return errorCode;
        }
    }
}
