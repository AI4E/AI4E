using System;
using System.Runtime.Serialization;

namespace AI4E.Blazor.Modularity
{
    [Serializable]
    public class ModulePrefixLookupException : Exception
    {
        public ModulePrefixLookupException()
        {
        }

        public ModulePrefixLookupException(string message) : base(message)
        {
        }

        public ModulePrefixLookupException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ModulePrefixLookupException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
