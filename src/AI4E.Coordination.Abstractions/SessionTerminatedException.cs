using System;
using System.Runtime.Serialization;

namespace AI4E.Coordination
{
    [Serializable]
    public class SessionTerminatedException : Exception
    {
        public SessionTerminatedException() { }

        public SessionTerminatedException(string message) : base(message) { }

        public SessionTerminatedException(string message, Exception innerException) : base(message, innerException) { }

        protected SessionTerminatedException(SerializationInfo info, StreamingContext context) : base(info, context) { }

        public SessionTerminatedException(Session session) : base(FormatMessage(session))
        {
            Session = session;
        }

        public SessionTerminatedException(Session session, Exception innerException) : base(FormatMessage(session), innerException)
        {
            Session = session;
        }

        public Session Session { get; }

        private static string FormatMessage(Session session)
        {
            return $"The session '{session.ToCompactString()}' is terminated.";
        }
    }
}
