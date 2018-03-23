using System;
using System.Runtime.Serialization;

namespace AI4E
{
    /// <summary>
    /// Thrown if an argument is the default value of its type or null if the type is a value type.
    /// </summary>
    public class ArgumentDefaultException : ArgumentException
    {
        private const string _defaultMessage = "A non-default value must be specified.";

        public ArgumentDefaultException() : base(_defaultMessage) { }

        public ArgumentDefaultException(string paramName) : base(_defaultMessage, paramName) { }

        public ArgumentDefaultException(string message, Exception innerException) : base(message, innerException) { }

        public ArgumentDefaultException(string message, string paramName) : base(message, paramName) { }

        public ArgumentDefaultException(string message, string paramName, Exception innerException) : base(message, paramName, innerException) { }

        protected ArgumentDefaultException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
