using System;
using System.Runtime.Serialization;

namespace Octgn.Communication
{
    [DataContract]
    [Serializable]
    public class HandshakeResult
    {
        [DataMember]
        public User User { get; set; }
        [DataMember]
        public bool Successful { get; set; }
        [DataMember]
        public string ErrorCode { get; set; }

        public static HandshakeResult Success(User user) {
            return new HandshakeResult {
                User = user,
                Successful = true
            };
        }

        public static HandshakeResult Failure(string error) {
            return new HandshakeResult {
                ErrorCode = error
            };
        }

        public override string ToString() {
            var result = Successful ? "Success" : ErrorCode;
            return $"{nameof(HandshakeResult)}: {User}: {result}";
        }
    }
}
