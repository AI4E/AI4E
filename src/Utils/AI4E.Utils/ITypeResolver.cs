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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;

namespace AI4E.Utils
{
    /// <summary>
    /// A abstraction for type loaders that can load types by there unqualified type-name.
    /// </summary>
    /// <remarks>
    /// Implementation of this interface should guarantee thread-safety for all members.
    /// </remarks>
    public interface ITypeResolver
    {
        /// <summary>
        /// Tries to load a type by its unqualified type name.
        /// </summary>
        /// <param name="unqualifiedTypeName">The unqualified type name of the type.</param>
        /// <param name="type">
        /// Contains the loaded <see cref="Type"/> if the operation was successful, <c>null</c> otherwise.
        /// </param>
        /// <returns>True if the type was loaded successfully, false otherwise.</returns>
        bool TryLoadType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type? type);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        /// <summary>
        /// Gets an instance of a type-loader that load types from the default context.
        /// </summary>
        public static ITypeResolver Default => DefaultTypeResolver.Instance;
#endif
    }

    /// <summary>
    /// Contains extension methods for the <see cref="ITypeResolver"/> interface.
    /// </summary>
    public static class TypeResolverExtension
    {
        /// <summary>
        /// Loads a type by its unqualified type name.
        /// </summary>
        /// <param name="resolver">The <see cref="ITypeResolver"/>.</param>
        /// <param name="unqualifiedTypeName">The unqualified type name of the type.</param>
        /// <returns>The loaded <see cref="Type"/>.</returns>
        /// <exception cref="ArgumentException">
        /// Thrown if the type specified by <paramref name="unqualifiedTypeName"/> cannot be loaded.
        /// </exception>
        public static Type LoadType(this ITypeResolver resolver, ReadOnlySpan<char> unqualifiedTypeName)
        {
#pragma warning disable CA1062
            if (!resolver.TryLoadType(unqualifiedTypeName, out var type))
#pragma warning restore CA1062
            {
                throw new ArgumentException($"Type '{unqualifiedTypeName.ToString()}' could not be loaded.");
            }

            return type!;
        }
    }

    internal sealed class DefaultTypeResolver : TypeResolver
    {
        public static DefaultTypeResolver Instance { get; } = new DefaultTypeResolver();

        private DefaultTypeResolver() : base(Enumerable.Empty<Assembly>(), fallbackToDefaultContext: true) { }
    }

    /// <summary>
    /// A type loader that loads types from a specified set of assemblies.
    /// </summary>
    /// <remarks>
    /// All member of this type are thread-safe.
    /// </remarks>
    public class TypeResolver : ITypeResolver
    {
        /// <summary>
        /// Gets an instance of a type-loader that load types from the default context.
        /// </summary>
        public static TypeResolver Default => DefaultTypeResolver.Instance;

        private readonly OrderedSet<Assembly> _assemblies;

        /// <summary>
        /// Creates a new instance of the <see cref="TypeResolver"/> class.
        /// </summary>
        /// <param name="assemblies">A collection of <see cref="Assembly"/> that types can be resolved from.</param>
        /// <param name="fallbackToDefaultContext">
        /// A boolean value indicating whether types are resolved from the default context as fallback.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assemblies"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="assemblies"/> contains <c>null</c> entries.
        /// </exception>
        public TypeResolver(IEnumerable<Assembly> assemblies, bool fallbackToDefaultContext)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            if (assemblies.Any(p => p is null))
                throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));

            _assemblies = new OrderedSet<Assembly>(assemblies);

            if (fallbackToDefaultContext)
            {
                _assemblies.UnionWith(AppDomain.CurrentDomain.GetAssemblies());
            }
        }

        /// <inheritdoc/>
        public bool TryLoadType(ReadOnlySpan<char> unqualifiedTypeName, [NotNullWhen(true)] out Type? type)
        {
            type = LoadTypeInternal(unqualifiedTypeName);

            return !(type is null);
        }

        private Type? LoadTypeInternal(ReadOnlySpan<char> chars)
        {
            if (chars.IndexOf('`', StringComparison.Ordinal) >= 0)
            {
                return LoadGenericType(chars);
            }

            return LoadNonGenericOrTypeDefinition(chars);
        }

        private Type? LoadTypeInternal(ReadOnlySpan<char> chars, Assembly assembly)
        {
            return assembly.GetType(chars.ToString(), false);
        }

        private Type? LoadNonGenericOrTypeDefinition(ReadOnlySpan<char> chars)
        {
            var openBracketIndex = chars.IndexOf('[', StringComparison.Ordinal);
            var baseTypeName = openBracketIndex < 0
                ? chars
                : chars.Slice(0, openBracketIndex);

            Type? result = null;

            foreach (var assembly in _assemblies)
            {
                result = LoadTypeInternal(baseTypeName, assembly);

                if (!(result is null))
                {
                    break;
                }
            }

            if (!(result is null) && openBracketIndex >= 0)
            {
                result = LoadArrayType(chars.Slice(openBracketIndex), result);
            }

            return result;
        }

        private Type? LoadGenericType(ReadOnlySpan<char> chars)
        {
            var openBracketIndex = chars.IndexOf('[', StringComparison.Ordinal);

            if (openBracketIndex < 0)
                return null;

            var genericTypeDefName = chars.Slice(0, openBracketIndex);
            var genericTypeDef = LoadNonGenericOrTypeDefinition(genericTypeDefName);

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
                        var genericTypeArgument = LoadTypeInternal(typeArgName);

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
                        var genericTypeArgument = LoadTypeInternal(typeArgName);

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
                            type = LoadArrayType(chars.Slice(i + 1, chars.Length - i - 1), type);
                        }

                        return type;
                    }
                }
            }

            return null;
        }

        private static Type? LoadArrayType(ReadOnlySpan<char> chars, Type type)
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

    internal static class MemoryExtensions
    {
        [ThreadStatic]
        private static string? _singleCharString = null;
        public static int IndexOf(in this ReadOnlySpan<char> chars, char c, StringComparison comparison)
        {
            if (chars.IsEmpty)
                return -1;

            _singleCharString ??= new string('\0', count: 1);

            var memory = MemoryMarshal.AsMemory(_singleCharString.AsMemory());
            memory.Span[0] = c;

#if SUPPORTS_SPAN_APIS
            return chars.IndexOf(memory.Span, comparison);
#else
            return chars.ToString().IndexOf(_singleCharString, comparison);
#endif
        }
    }
}
