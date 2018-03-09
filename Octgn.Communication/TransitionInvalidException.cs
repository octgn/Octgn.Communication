using System;

namespace Octgn.Communication
{
    [Serializable]
    public class TransitionInvalidException : Exception
    {
        public TransitionInvalidException(ConnectionState oldState, ConnectionState newState)
            : base ($"Transition from {oldState} to {newState} is invalid.") {

        }

        public TransitionInvalidException() { }
        public TransitionInvalidException(string message) : base(message) { }
        public TransitionInvalidException(string message, Exception inner) : base(message, inner) { }
        protected TransitionInvalidException(
          System.Runtime.Serialization.SerializationInfo info,
          System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
