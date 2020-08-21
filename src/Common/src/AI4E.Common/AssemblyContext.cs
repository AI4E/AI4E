/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Runtime.Loader;

namespace AI4E
{
    internal readonly struct AssemblyContext : IEquatable<AssemblyContext>
    {
        public static AssemblyContext Empty { get; } = new AssemblyContext(null, null);

        public static AssemblyContext Create(AssemblyLoadContext? assemblyLoadContext, IServiceProvider? assemblyServiceProvider)
        {
            // Do not create a new instance, if not necessary.
            if (assemblyLoadContext is null &&
               assemblyServiceProvider is null)
            {
                return Empty;
            }

            return new AssemblyContext(assemblyLoadContext, assemblyServiceProvider);
        }

        private AssemblyContext(AssemblyLoadContext? assemblyLoadContext, IServiceProvider? assemblyServiceProvider)
        {
            AssemblyLoadContext = assemblyLoadContext;
            AssemblyServiceProvider = assemblyServiceProvider;
        }

        public AssemblyLoadContext? AssemblyLoadContext { get; }
        public IServiceProvider? AssemblyServiceProvider { get; }

        public bool Equals(AssemblyContext other)
        {
            return AssemblyLoadContext == other.AssemblyLoadContext &&
                   AssemblyServiceProvider == other.AssemblyServiceProvider;
        }

        public override bool Equals(object? obj)
        {
            return obj is AssemblyContext assemblyContext && Equals(assemblyContext);
        }

        public override int GetHashCode()
        {
            return (AssemblyLoadContext, AssemblyServiceProvider).GetHashCode();
        }

        public static bool operator ==(AssemblyContext left, AssemblyContext right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(AssemblyContext left, AssemblyContext right)
        {
            return !left.Equals(right);
        }
    }
}
