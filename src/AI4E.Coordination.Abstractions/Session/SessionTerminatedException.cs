using System;
using System.Runtime.Serialization;

namespace AI4E.Coordination.Session
{
    [Serializable]
    public class SessionTerminatedException : Exception
    {
        public SessionTerminatedException() { }

        public SessionTerminatedException(string message) : base(message) { }

        public SessionTerminatedException(string message, Exception innerException) : base(message, innerException) { }

        protected SessionTerminatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public SessionTerminatedException(CoordinationSession session) : base(FormatMessage(session))
        {
            Session = session;
        }

        public SessionTerminatedException(CoordinationSession session, Exception innerException) : base(FormatMessage(session), innerException)
        {
            Session = session;
        }

        public CoordinationSession Session { get; }

        private static string FormatMessage(CoordinationSession session)
        {
            return $"The session '{session.ToString()}' is terminated.";
        }
    }
}
