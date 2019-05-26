using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AI4E.Utils;

namespace AI4E.Modularity.Metadata
{
    public readonly struct ModuleDependency : IEquatable<ModuleDependency>
    {
        public static ModuleDependency Unknown { get; } = default;

        public ModuleDependency(ModuleIdentifier module, ModuleVersionRange versionRange)
        {
            if (module == ModuleIdentifier.UnknownModule)
            {
                this = Unknown;
            }
            else
            {
                Module = module;
                VersionRange = versionRange;
            }
        }

        public ModuleIdentifier Module { get; }
        public ModuleVersionRange VersionRange { get; }

        public bool Equals(ModuleDependency other)
        {
            return other.Module == Module &&
                   other.VersionRange == VersionRange;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleDependency dependency && Equals(dependency);
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode() ^ VersionRange.GetHashCode();
        }

        public static bool operator ==(ModuleDependency left, ModuleDependency right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleDependency left, ModuleDependency right)
        {
            return !left.Equals(right);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Deconstruct(out ModuleIdentifier module, out ModuleVersionRange versionRange)
        {
            module = Module;
            versionRange = VersionRange;
        }
    }
}
