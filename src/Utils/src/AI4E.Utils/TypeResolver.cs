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
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;

namespace AI4E.Utils
{
    /// <summary>
    /// A type resolver that resolved types from a specified set of assemblies.
    /// </summary>
    /// <remarks>
    /// All member of this type are thread-safe.
    /// </remarks>
    public class TypeResolver : ITypeResolver
    {
        /// <summary>
        /// Gets an instance of a type-resolver that resolved types from the default context.
        /// </summary>
        public static TypeResolver Default => DefaultTypeResolver.Instance;

        private readonly ImmutableList<Assembly> _assemblies;
        private readonly bool _fallbackToDefaultContext;

        // An unordered set of the assemblies that is used to get a distinct list of assemblies.
        // This is null, if _fallbackToDefaultContext is false and _assemblies is not empty.
        private readonly ImmutableHashSet<Assembly>? _assemblyHashSet;

        protected TypeResolver() : this(Enumerable.Empty<Assembly>()) { }

        public TypeResolver(IEnumerable<Assembly> assemblies, bool fallbackToDefaultContext = true)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            _assemblies = assemblies.Distinct(AssemblyByDisplayNameComparer.Instance).ToImmutableList();

            if (_assemblies.Any(p => p is null))
            {
                throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));
            }

            if (_assemblies.Any() && fallbackToDefaultContext)
            {
                _assemblyHashSet = _assemblies.ToImmutableHashSet();
            }

            _fallbackToDefaultContext = fallbackToDefaultContext;
        }

        /// <inheritdoc/>
        public bool TryResolveType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type? type)
        {
            type = ResolveTypeInternal(unqualifiedTypeName);

            return !(type is null);
        }

        private Type? ResolveTypeInternal(ReadOnlySpan<char> unqualifiedTypeName)
        {
            if (unqualifiedTypeName.IndexOf('`') >= 0)
            {
                return ResolveGenericType(unqualifiedTypeName);
            }

            return ResolveNonGenericOrTypeDefinition(unqualifiedTypeName);
        }

        protected virtual IEnumerable<Assembly> ReflectAssemblies()
        {
            if (!_fallbackToDefaultContext)
            {
                return _assemblies;
            }

            var defaultAssemblies = (IEnumerable<Assembly>)AppDomain.CurrentDomain.GetAssemblies();

            // This is a common case, so we optimize for this.
            if (!_assemblies.Any())
            {
                // We can just return the mutable array, as we control the caller.
                return defaultAssemblies;
            }

            Debug.Assert(_assemblyHashSet != null);

            // Get all assemblies from defaultAssemblies that is not present in _assemblies already.
            defaultAssemblies = defaultAssemblies.Where(p => !_assemblyHashSet!.Contains(p));

            return _assemblies.Concat(defaultAssemblies);
        }

        protected virtual Type? ResolveTypeInternal(ReadOnlySpan<char> unqualifiedTypeName, Assembly reflectedAssembly)
        {
            if (reflectedAssembly is null)
                throw new ArgumentNullException(nameof(reflectedAssembly));

            return reflectedAssembly.GetType(unqualifiedTypeName.ToString(), false);
        }

        private Type? ResolveNonGenericOrTypeDefinition(ReadOnlySpan<char> chars)
        {
            var openBracketIndex = chars.IndexOf('[');
            var baseTypeName = openBracketIndex < 0
                ? chars
                : chars.Slice(0, openBracketIndex);

            Type? result = null;

            foreach (var assembly in ReflectAssemblies())
            {
                result = ResolveTypeInternal(baseTypeName, assembly);

                if (!(result is null))
                {
                    break;
                }
            }

            if (!(result is null) && openBracketIndex >= 0)
            {
                result = ResolveArrayType(chars.Slice(openBracketIndex), result);
            }

            return result;
        }

        private Type? ResolveGenericType(ReadOnlySpan<char> chars)
        {
            var openBracketIndex = chars.IndexOf('[');

            if (openBracketIndex < 0)
                return null;

            var genericTypeDefName = chars.Slice(0, openBracketIndex);
            var genericTypeDef = ResolveNonGenericOrTypeDefinition(genericTypeDefName);

            if (genericTypeDef is null)
                return null;

            var genericTypeArguments = new List<Type>();
            var typeArgStartIndex = openBracketIndex + 1;
            var scope = 0;

            for (var i = openBracketIndex; i < chars.Length; ++i)
            {
                var current = chars[i];
                switch (current)
                {
                    case '[':
                    {
                        ++scope;
                        break;
                    }
                    case ',':
                    {
                        if (scope != 1)
                            break;

                        var typeArgName = chars.Slice(typeArgStartIndex, i - typeArgStartIndex);
                        var genericTypeArgument = ResolveTypeInternal(typeArgName);

                        if (genericTypeArgument is null)
                        {
                            return null;
                        }

                        genericTypeArguments.Add(genericTypeArgument);
                        typeArgStartIndex = i + 1;

                        break;
                    }
                    case ']':
                    {
                        --scope;

                        if (scope != 0)
                            break;

                        var typeArgName = chars.Slice(typeArgStartIndex, i - typeArgStartIndex);
                        var genericTypeArgument = ResolveTypeInternal(typeArgName);

                        if (genericTypeArgument is null)
                        {
                            return null;
                        }

                        genericTypeArguments.Add(genericTypeArgument);

#pragma warning disable IDE0004
                        var type = (Type?)genericTypeDef.MakeGenericType(genericTypeArguments.ToArray());
#pragma warning restore IDE0004

                        if (!(type is null) && i + 1 < chars.Length)
                        {
                            type = ResolveArrayType(chars.Slice(i + 1, chars.Length - i - 1), type);
                        }

                        return type;
                    }
                }
            }

            return null;
        }

        private static Type? ResolveArrayType(ReadOnlySpan<char> chars, Type type)
        {
            if (chars.IsEmpty)
                return type;

            var rank = 1;
            var started = false;
            var ended = false;

            for (var i = 0; i < chars.Length; i++)
            {
                var current = chars[i];

                switch (current)
                {
                    case '[':
                    {
                        if (started || ended)
                            return null;

                        started = true;
                        break;
                    }
                    case ']':
                    {
                        if (!started || ended)
                            return null;

                        ended = true;
                        break;
                    }
                    case ',':
                    {
                        if (!started || ended)
                            return null;

                        rank++;
                        break;
                    }
                    default:
                    {
                        if (!char.IsWhiteSpace(current))
                            return null;

                        break;
                    }
                }
            }

            if (rank == 1)
            {
                type = type.MakeArrayType();
            }
            else
            {
                type = type.MakeArrayType(rank);
            }

            return type;
        }
    }

    /// <summary>
    /// Contains extension methods for the <see cref="ITypeResolver"/> interface.
    /// </summary>
    public static class TypeResolverExtension
    {
        /// <summary>
        /// Resolved a type by its unqualified type name.
        /// </summary>
        /// <param name="resolver">The <see cref="ITypeResolver"/>.</param>
        /// <param name="unqualifiedTypeName">The unqualified type name of the type.</param>
        /// <returns>The resolved <see cref="Type"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the type specified by <paramref name="unqualifiedTypeName"/> cannot be resolved.
        /// </exception>
        public static Type ResolveType(this ITypeResolver resolver, ReadOnlySpan<char> unqualifiedTypeName)
        {
#pragma warning disable CA1062
            if (!resolver.TryResolveType(unqualifiedTypeName, out var type))
#pragma warning restore CA1062
            {
                // TODO: Add a dedicated exception type for this?
                throw new ArgumentException($"Type '{unqualifiedTypeName.ToString()}' could not be resolved.");
            }

            return type!;
        }
    }

    internal sealed class DefaultTypeResolver : TypeResolver
    {
        public static DefaultTypeResolver Instance { get; } = new DefaultTypeResolver();
    }
}
