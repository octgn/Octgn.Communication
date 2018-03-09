using System;

namespace Octgn.Communication
{
    public static class ErrorResponseCodes
    {
        public const string UnauthorizedRequest = nameof(UnauthorizedRequest);
        public const string UserOffline = nameof(UserOffline);
        public const string UnhandledServerError = nameof(UnhandledServerError);
        public const string HandshakeFailed = nameof(HandshakeFailed);
        public const string UnhandledRequest = nameof(UnhandledRequest);
    }
}
