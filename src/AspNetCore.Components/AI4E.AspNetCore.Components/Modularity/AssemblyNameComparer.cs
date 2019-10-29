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
using System.Collections.Generic;
using System.Reflection;

namespace AI4E.AspNetCore.Components.Modularity
{
    /// <summary>
    /// An equality comparer that compares assembly names via their full-name property.
    /// </summary>
    internal class AssemblyNameComparer : IEqualityComparer<AssemblyName>
    {
        public static AssemblyNameComparer Instance { get; } = new AssemblyNameComparer();

        private AssemblyNameComparer() { }

        public bool Equals(AssemblyName x, AssemblyName y)
        {
            return string.Equals(x?.FullName, y?.FullName, StringComparison.Ordinal);
        }

        public int GetHashCode(AssemblyName obj)
        {
            return obj.FullName.GetHashCode(StringComparison.Ordinal);
        }
    }
}
