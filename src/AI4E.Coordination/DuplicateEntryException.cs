using System;
using System.Runtime.Serialization;

namespace AI4E.Coordination
{
    public class DuplicateEntryException : Exception
    {
        public DuplicateEntryException() : base("An entry with the specified key is already present") { }

        public DuplicateEntryException(string key) : base($"An entry with the key '{key}' is already present") { }

        public DuplicateEntryException(string message, Exception innerException) : base(message, innerException) { }

        protected DuplicateEntryException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
