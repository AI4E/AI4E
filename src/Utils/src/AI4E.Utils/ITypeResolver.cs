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

namespace AI4E.Utils
{
    /// <summary>
    /// An abstraction for type resolvers that can resolve types by there unqualified type-name.
    /// </summary>
    /// <remarks>
    /// Implementation of this interface should guarantee thread-safety for all members.
    /// </remarks>
    public interface ITypeResolver
    {
        /// <summary>
        /// Tries to resolve a type by its unqualified type name.
        /// </summary>
        /// <param name="unqualifiedTypeName">The unqualified type name of the type.</param>
        /// <param name="type">
        /// Contains the resolved <see cref="Type"/> if the operation was successful, <c>null</c> otherwise.
        /// </param>
        /// <returns>True if the type was resolved successfully, false otherwise.</returns>
        bool TryResolveType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type? type);

        /// <summary>
        /// Gets an instance of a type-resolver that resolves types from the default context.
        /// </summary>
        public static ITypeResolver Default => DefaultTypeResolver.Instance;
    }
}
