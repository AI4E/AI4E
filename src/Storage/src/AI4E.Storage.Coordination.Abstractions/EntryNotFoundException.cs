using System;
using System.Runtime.Serialization;
using AI4E.Utils.Memory;

namespace AI4E.Storage.Coordination
{
    public class EntryNotFoundException : Exception
    {
        public EntryNotFoundException() : base("An entry with the specified path is not present.") { }

        public EntryNotFoundException(CoordinationEntryPath path) : base($"An entry with the path '{path.EscapedPath.ConvertToString()}' is not present.") { }

        public EntryNotFoundException(string message, Exception innerException) : base(message, innerException) { }

        protected EntryNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
