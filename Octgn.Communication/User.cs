using System;
using System.Diagnostics;
using System.Runtime.Serialization;

namespace Octgn.Communication
{
    [DataContract]
    [Serializable]
    public class User : IEquatable<User>
    {
        public User() { }

        public User(string userId) {
            Id = userId;
        }

        public User(string userId, string displayName) {
            if (string.IsNullOrWhiteSpace(userId)) throw new ArgumentNullException(nameof(userId));

            Id = userId;
            DisplayName = displayName;
        }

        [DataMember]
        public string Id { get; set; }

        [DataMember]
        public string DisplayName { get; set; }

        public override string ToString() {
            return $"{DisplayName}#{Id}";
        }

        [DebuggerStepThrough]
        public static User Parse(string userString) {
            if (string.IsNullOrWhiteSpace(userString)) throw new ArgumentNullException(nameof(userString));

            var parts = userString.Split('#');
            if (parts.Length != 2)
                throw new InvalidOperationException($"'{userString}' is not a valid User");

            var ret = new User {
                DisplayName = parts[0],
                Id = parts[1]
            };

            if (string.IsNullOrWhiteSpace(ret.Id))
                throw new InvalidOperationException($"'{userString}' is not a valid User");

            return ret;
        }

        public static bool TryParse(string userString, out User user) {
            try {
                if (string.IsNullOrWhiteSpace(userString)) {
                    user = null;
                    return false;
                }
                user = Parse(userString);
                return true;
            } catch {
                user = null;
                return false;
            }
        }

        public bool Equals(User other) {
            return string.Equals(Id, other?.Id, StringComparison.InvariantCultureIgnoreCase);
        }

        public override bool Equals(object obj) {
            if (obj == null) return false;
            if (obj is User user) return Equals(user);
            return false;
        }

        public override int GetHashCode() {
            return Id.GetHashCode();
        }
    }
}
