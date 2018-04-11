using System;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Text;

namespace AI4E.Coordination
{
    public class SessionTerminatedException : Exception
    {
        public SessionTerminatedException()
        {
        }

        public SessionTerminatedException(string message) : base(message)
        {
        }

        public SessionTerminatedException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected SessionTerminatedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
