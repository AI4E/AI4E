using System;
using System.Runtime.Serialization;

namespace AI4E.Modularity.Module
{
    public class ModuleServerException : Exception
    {
        public ModuleServerException()
        {
        }

        public ModuleServerException(string message) : base(message)
        {
        }

        public ModuleServerException(string message, Exception innerException) : base(message, innerException)
        {
        }

        protected ModuleServerException(SerializationInfo info, StreamingContext context) : base(info, context)
        {
        }
    }
}
