/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using Newtonsoft.Json;

namespace AI4E.Modularity
{
    public readonly struct ModuleReleaseIdentifier : IEquatable<ModuleReleaseIdentifier>
    {
        public static ModuleReleaseIdentifier UnknownModuleRelease { get; } = default;

        public ModuleReleaseIdentifier(ModuleIdentifier module, ModuleVersion version)
        {
            if (module == ModuleIdentifier.UnknownModule || version == ModuleVersion.Unknown)
            {
                this = UnknownModuleRelease;
            }
            else
            {
                Module = module;
                Version = version;
            }
        }

        //public ModuleReleaseIdentifier(string name, ModuleVersion version)
        //{
        //    if (version == ModuleVersion.Unknown)
        //    {
        //        this = default;
        //    }
        //    else
        //    {
        //        Module = new ModuleIdentifier(name);
        //        Version = version;
        //    }
        //}

        // TODO: These should be read-only. 
        //       The in-memory database and json.net have problems to recreate the object.
        //       Maybe a custom type converter can help here.
        [JsonProperty]
        public ModuleIdentifier Module { get; }

        [JsonProperty]
        public ModuleVersion Version { get; }

        public bool Equals(ModuleReleaseIdentifier other)
        {
            return other.Module == Module &&
                   other.Version == Version;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleReleaseIdentifier id && Equals(id);
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode() ^ Version.GetHashCode();
        }

        public override string ToString()
        {
            if (this == UnknownModuleRelease)
                return "Unknown module release";

            return $"{Module} {Version}";
        }

        public static bool operator ==(ModuleReleaseIdentifier left, ModuleReleaseIdentifier right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleReleaseIdentifier left, ModuleReleaseIdentifier right)
        {
            return !left.Equals(right);
        }
    }
}
