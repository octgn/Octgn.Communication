namespace Octgn.Communication
{
    public class User
    {
        private static ILogger Log = LoggerFactory.Create(nameof(User));
        public string NodeId { get; set; }

        public string Status { get; set; }

        public User() {

        }

        public User(string nodeId) {
            NodeId = nodeId;
        }

        public override string ToString() {
            return NodeId;
        }

        public const string OfflineStatus = "offline";
        public const string OnlineStatus = "online";
        public const string AwayStatus = "away";
        public const string BusyStatus = "busy";
        public const string BlockedStatus = "blocked";
    }
}
