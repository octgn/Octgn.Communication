using System;
using System.Diagnostics;

namespace Octgn.Communication
{
    [Serializable]
    public class User
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

        public string Id { get; set; }
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

        [DebuggerStepThrough]
        public static bool TryParse(string userString, out User user) {
            try {
                user = Parse(userString);
                return true;
            } catch {
                user = null;
                return false;
            }
        }
    }
}
