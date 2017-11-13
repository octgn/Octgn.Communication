namespace Octgn.Communication
{
    public class AuthenticationResult
    {
        public User User { get; set; }
        public bool Successful { get; set; }
        public string ErrorCode { get; set; }

        public static AuthenticationResult Success(User user) {
            return new AuthenticationResult {
                User = user,
                Successful = true
            };
        }
    }
}
