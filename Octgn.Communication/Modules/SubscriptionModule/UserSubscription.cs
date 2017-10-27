using Octgn.Communication.Packets;

namespace Octgn.Communication.Modules.SubscriptionModule
{
    public class UserSubscription
    {
        public string Id { get; set; }
        public string SubscriberUserId { get; set; }
        public string UserId { get; set; }
        public string Category { get; set; }
        public UpdateType UpdateType { get; set; }

        public UserSubscription() { }

        public static UserSubscription GetFromPacket(DictionaryPacket packet) {
            return (UserSubscription)packet["subscription"];
        }

        public static void AddToPacket(DictionaryPacket packet, UserSubscription subscription) {
            packet["subscription"] = subscription;
        }

        public static string GetIdFromPacket(DictionaryPacket packet) {
            return (string)packet["usubid"];
        }

        public static void AddIdToPacket(DictionaryPacket packet, string id) {
            packet["usubid"] = id;
        }
    }
}
