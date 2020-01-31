using System;
using System.Runtime.Serialization;
using AI4E.Utils.Memory;

namespace AI4E.Storage.Coordination
{
    public class DuplicateEntryException : Exception
    {
        public DuplicateEntryException() : base("An entry with the specified path is already present") { }

        public DuplicateEntryException(CoordinationEntryPath path) : base($"An entry with the path '{path.EscapedPath.ConvertToString()}' is already present") { }

        public DuplicateEntryException(string message, Exception innerException) : base(message, innerException) { }

        protected DuplicateEntryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
