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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * AspNet Core (https://github.com/aspnet/AspNetCore)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Linq;
using System.Reflection;
using AI4E.Utils;

namespace AI4E
{
    public sealed class HasReferenceAssemblyResolver : AssemblyResolverBase
    {
        private readonly AssemblyName _referenceName;
        private readonly AssemblyNameComparer _comparer;

        public HasReferenceAssemblyResolver(AssemblyName referenceName, AssemblyNameComparer comparer)
        {
            if (referenceName is null)
                throw new ArgumentNullException(nameof(referenceName));

            if (comparer is null)
                throw new ArgumentNullException(nameof(comparer));

            _referenceName = referenceName;
            _comparer = comparer;
        }

        protected override bool MatchesCondition(Assembly assembly)
        {
            return assembly.GetReferencedAssemblies().Any(r => _comparer.Equals(r, _referenceName));
        }
    }
}
