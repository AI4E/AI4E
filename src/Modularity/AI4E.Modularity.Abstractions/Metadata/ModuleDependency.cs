/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

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
