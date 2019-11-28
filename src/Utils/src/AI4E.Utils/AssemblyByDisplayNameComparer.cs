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

namespace AI4E.Utils
{
    public sealed class AssemblyByDisplayNameComparer : IEqualityComparer<Assembly>
    {
        public static AssemblyByDisplayNameComparer Instance { get; } = new AssemblyByDisplayNameComparer();

        private AssemblyByDisplayNameComparer() { }

        public bool Equals(Assembly? x, Assembly? y)
        {
            var displayNameX = x?.FullName;
            var displayNameY = y?.FullName;

            return StringComparer.OrdinalIgnoreCase.Equals(displayNameX, displayNameY);
        }

        public int GetHashCode(Assembly obj)
        {
            if (obj is null)
#pragma warning disable CA1065
                throw new ArgumentNullException(nameof(obj));
#pragma warning restore CA1065

            var displayName = obj.FullName;

            return displayName is null 
                ? 0 
                : StringComparer.OrdinalIgnoreCase.GetHashCode(displayName);
        }   
    }
}
