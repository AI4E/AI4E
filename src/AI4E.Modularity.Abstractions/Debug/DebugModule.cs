using System;
using System.Collections.Generic;
using AI4E.Routing;

namespace AI4E.Modularity.Debug
{
    public sealed class DebugModule
    {
        public DebugModule(EndPointAddress endPoint, ModuleIdentifier module, ModuleVersion moduleVersion)
        {
            EndPoint = endPoint;
            Module = module;
            ModuleVersion = moduleVersion;
        }

        public EndPointAddress EndPoint { get; }
        public ModuleIdentifier Module { get; }
        public ModuleVersion ModuleVersion { get; }
    }

    public sealed class DebugModuleEqualityComparer : IEqualityComparer<DebugModule>
    {
        private DebugModuleEqualityComparer() { }

        public bool Equals(DebugModule x, DebugModule y)
        {
            if (ReferenceEquals(x, y))
                return true;

            if (x is null)
                return y is null;

            if (y is null)
                return false;

            return x.EndPoint == y.EndPoint &&
                   x.Module == y.Module &&
                   x.ModuleVersion == y.ModuleVersion;
        }

        public int GetHashCode(DebugModule obj)
        {
            if (obj is null)
                return 0;

            return ((obj.EndPoint.GetHashCode() * 12323) + obj.Module.GetHashCode() * 12323) + obj.ModuleVersion.GetHashCode();
        }

        [ThreadStatic]
        private static DebugModuleEqualityComparer _debugModuleEqualityComparer;

        public static DebugModuleEqualityComparer Instance
        {
            get
            {
                if (_debugModuleEqualityComparer == null)
                    _debugModuleEqualityComparer = new DebugModuleEqualityComparer();

                return _debugModuleEqualityComparer;
            }
        }
    }
}
