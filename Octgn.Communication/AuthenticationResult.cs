namespace Octgn.Communication
{
    public class AuthenticationResult
    {
        public string UserId { get; set; }
        public bool Successful { get; set; }
        public string ErrorCode { get; set; }

        public static AuthenticationResult Success(string userId) {
            return new AuthenticationResult {
                UserId = userId,
                Successful = true
            };
        }
    }
}
