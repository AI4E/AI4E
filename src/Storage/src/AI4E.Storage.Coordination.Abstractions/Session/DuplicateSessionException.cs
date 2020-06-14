using System;
using System.Runtime.Serialization;

namespace AI4E.Storage.Coordination.Session
{
    public class DuplicateSessionException : Exception
    {
        public DuplicateSessionException()
        {
        }

        public DuplicateSessionException(string message) : base(message)
        {
        }

        public DuplicateSessionException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected DuplicateSessionException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
