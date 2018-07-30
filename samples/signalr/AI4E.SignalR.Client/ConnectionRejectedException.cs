using System;
using System.Runtime.Serialization;

namespace AI4E.SignalR.Client
{
    [Serializable]
    public class ConnectionRejectedException : Exception
    {
        public ConnectionRejectedException() : base("The connection was rejected for an unknown reason.")
        {
            RejectReason = RejectReason.Unknown;
        }

        public ConnectionRejectedException(RejectReason reason) : base("The connection was rejected.")
        {
            RejectReason = reason;
        }

        public ConnectionRejectedException(string message) : base(message)
        {
            RejectReason = RejectReason.Unknown;
        }

        public ConnectionRejectedException(string message, Exception innerException) : base(message, innerException)
        {
            RejectReason = RejectReason.Unknown;
        }

        protected ConnectionRejectedException(SerializationInfo info, StreamingContext context) : base(info, context)
        {

        }

        public RejectReason RejectReason { get; }
    }
}
