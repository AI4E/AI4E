using System;
using System.Collections.Generic;
using System.Linq;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugModuleQueryHandler : MessageHandler
    {
        private readonly DebugPort _debugPort;

        public DebugModuleQueryHandler(DebugPort debugPort)
        {
            if (debugPort == null)
                throw new ArgumentNullException(nameof(debugPort));

            _debugPort = debugPort;
        }

        public IEnumerable<DebugModule> Handle(Query<IEnumerable<DebugModule>> message)
        {
            return _debugPort.DebugSessions
                             .Select(p => new DebugModule(p.EndPoint, p.Module, p.ModuleVersion))
                             .Distinct(DebugModuleEqualityComparer.Instance)
                             .ToList();
        }
    }
}
