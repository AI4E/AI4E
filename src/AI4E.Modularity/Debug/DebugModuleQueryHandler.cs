using System;
using System.Collections.Generic;

namespace AI4E.Modularity.Debug
{
    [MessageHandler]
    internal sealed class DebugModuleQueryHandler : MessageHandler
    {
        private readonly DebugPort _debugPort;

        public DebugModuleQueryHandler(DebugPort debugPort)
        {
            if (debugPort == null)
                throw new ArgumentNullException(nameof(debugPort));

            _debugPort = debugPort;
        }

        public IEnumerable<DebugModuleProperties> Handle(Query<IEnumerable<DebugModuleProperties>> message)
        {
            return _debugPort.ConnectedDebugModules;
        }
    }
}
