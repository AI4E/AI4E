using System;
using System.Runtime.Serialization;

namespace AI4E.Coordination
{
    public class EntryNotFoundException : Exception
    {
        public EntryNotFoundException() : base("An entry with the specified key is not present.") { }

        public EntryNotFoundException(string key) : base($"An entry with the key '{key}' is not present.") { }

        public EntryNotFoundException(string message, Exception innerException) : base(message, innerException) { }

        protected EntryNotFoundException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
