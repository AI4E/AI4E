using System;
using System.Runtime.Serialization;

namespace AI4E.Modularity
{
    [Serializable]
    public class ModuleUninstallationException : Exception
    {
        public ModuleUninstallationException() { }

        public ModuleUninstallationException(string message) : base(message) { }

        public ModuleUninstallationException(string message, Exception innerException) : base(message, innerException) { }

        protected ModuleUninstallationException(SerializationInfo info, StreamingContext context) : base(info, context) { }
    }
}
