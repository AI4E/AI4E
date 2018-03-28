using System;
using System.Runtime.Serialization;

namespace AI4E.Modularity.EndPointManagement
{
    [Serializable]
    public class EndPointNotFoundException : Exception
    {
        public EndPointNotFoundException() { }

        public EndPointNotFoundException(string message) : base(message) { }

        public EndPointNotFoundException(string message, Exception innerException) : base(message, innerException) { }

        protected EndPointNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
