using System;
using System.Runtime.Serialization;

namespace AI4E.Modularity.Metadata
{
    [Serializable]
    public class ModuleMetadataFormatException : FormatException
    {
        public ModuleMetadataFormatException() { }

        public ModuleMetadataFormatException(string message) : base(message) { }

        public ModuleMetadataFormatException(string message, Exception innerException) : base(message, innerException) { }

        protected ModuleMetadataFormatException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
