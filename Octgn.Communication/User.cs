namespace Octgn.Communication
{
    public class User
    {
        private static ILogger Log = LoggerFactory.Create(nameof(User));
        public string UserId { get; set; }
        public string DisplayName { get; set; }

        public string Status { get; set; }

        public User() {

        }

        public User(string userId) {
            UserId = userId;
        }

        public override string ToString() {
            return $"{DisplayName}#{UserId}";
        }

        public const string OfflineStatus = "offline";
        public const string OnlineStatus = "online";
        public const string AwayStatus = "away";
        public const string BusyStatus = "busy";
        public const string BlockedStatus = "blocked";
    }
}
