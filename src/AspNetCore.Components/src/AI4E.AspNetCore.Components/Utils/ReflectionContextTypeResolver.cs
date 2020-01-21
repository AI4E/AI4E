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
using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace AI4E.Utils
{
    internal sealed class ReflectionContextTypeResolver : ITypeResolver
    {
        private readonly ITypeResolver _typeResolver;
        private readonly ReflectionContext _reflectionContext;

        public ReflectionContextTypeResolver(
            ITypeResolver typeResolver,
            ReflectionContext reflectionContext)
        {
            if (typeResolver is null)
                throw new ArgumentNullException(nameof(typeResolver));

            if (reflectionContext is null)
                throw new ArgumentNullException(nameof(reflectionContext));

            _typeResolver = typeResolver;
            _reflectionContext = reflectionContext;
        }

        public bool TryResolveType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type? type)
        {
            if (!_typeResolver.TryResolveType(unqualifiedTypeName, out type))
            {
                return false;
            }

            type = _reflectionContext.MapType(type.GetTypeInfo());
            return true;
        }
    }
}
