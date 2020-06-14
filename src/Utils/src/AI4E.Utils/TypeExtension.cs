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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * Scrutor (https://github.com/khellang/Scrutor)
 * The MIT License
 * 
 * Copyright (c) 2015 Kristian Hellang
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * .Net Extensions (https://github.com/aspnet/Extensions)
 * Copyright (c) .NET Foundation. All rights reserved.
 * Licensed under the Apache License, Version 2.0. See License.txt in the project root for license information.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace System
{
    public static class AI4EUtilsTypeExtension
    {
        public static bool IsDefined<TCustomAttribute>(
            this Type type,
            bool inherit = false)
            where TCustomAttribute : Attribute
        {
#pragma warning disable CA1062
            return (type as ICustomAttributeProvider).IsDefined<TCustomAttribute>(inherit);
#pragma warning restore CA1062
        }

        public static TCustomAttribute[] GetCustomAttributes<TCustomAttribute>(
            this Type type,
            bool inherit = false)
            where TCustomAttribute : Attribute
        {
#pragma warning disable CA1062
            return (type as ICustomAttributeProvider).GetCustomAttributes<TCustomAttribute>(inherit);
#pragma warning restore CA1062
        }

        // This is a conditional weak table to allow assembly unloading.
        private static readonly ConditionalWeakTable<Type, ImmutableList<PropertyInfo>> _publicPropertyLookup
            = new ConditionalWeakTable<Type, ImmutableList<PropertyInfo>>();

        public static IReadOnlyList<PropertyInfo> GetPublicProperties(this Type type)
        {
#pragma warning disable CA1062
            if (!type.IsInterface)
#pragma warning restore CA1062
                return type.GetProperties(BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);

            return _publicPropertyLookup.GetValue(type, BuildPublicPropertyList);
        }

        private static ImmutableList<PropertyInfo> BuildPublicPropertyList(Type type)
        {
            var propertyInfos = ImmutableList.CreateBuilder<PropertyInfo>();

            var considered = new List<Type>();
            var queue = new Queue<Type>();
            considered.Add(type);
            queue.Enqueue(type);

            while (queue.Count > 0)
            {
                var subType = queue.Dequeue();
                foreach (var subInterface in subType.GetInterfaces())
                {
                    if (considered.Contains(subInterface))
                        continue;

                    considered.Add(subInterface);
                    queue.Enqueue(subInterface);
                }

                var typeProperties = subType.GetProperties(
                    BindingFlags.FlattenHierarchy | BindingFlags.Public | BindingFlags.Instance);

                var newPropertyInfos = typeProperties.Where(x => !propertyInfos.Contains(x));

                propertyInfos.InsertRange(0, newPropertyInfos);
            }

            return propertyInfos.ToImmutable();
        }

        public static string GetUnqualifiedTypeName(this Type type)
        {
#pragma warning disable CA1062
            return type.ToString();
#pragma warning restore CA1062
        }

        public static bool IsDelegate(this Type type)
        {
#pragma warning disable CA1062
            if (type.IsValueType)
#pragma warning restore CA1062
                return false;

            if (type.IsInterface)
                return false;

            if (type == typeof(Delegate) || type == typeof(MulticastDelegate))
                return true;

            return type.IsSubclassOf(typeof(Delegate));
        }

        /// <summary>
        /// Checks whether the type is an ordinary class and specially not any of:
        /// - A value type
        /// - A delegate
        /// - A generic type definition
        /// - The type <see cref="object"/>, <see cref="Enum"/> or <see cref="ValueType"/>
        /// </summary>
        /// <param name="type">The type to check.</param>
        /// <returns>True if <paramref name="type"/> is an ordinary class, false otherwise.</returns>
        public static bool IsOrdinaryClass(this Type type)
        {
#pragma warning disable CA1062
            if (type.IsValueType)
#pragma warning restore CA1062
                return false;

            if (type.IsDelegate())
                return false;

            if (type.IsGenericTypeDefinition)
                return false;

            // TODO: Do we have to check for System.Void? It is defined as a value type, that we alread check for.
            if (type == typeof(Enum) || type == typeof(ValueType) || type == typeof(void))
                return false;

            return true;
        }

        // Based on: https://github.com/khellang/Scrutor/blob/master/src/Scrutor/ReflectionExtensions.cs
        public static bool IsAssignableTo(this Type type, Type otherType)
        {
            var typeInfo = type.GetTypeInfo();
            var otherTypeInfo = otherType.GetTypeInfo();

            if (otherTypeInfo.IsGenericTypeDefinition)
            {
                return typeInfo.IsAssignableToGenericTypeDefinition(otherTypeInfo);
            }

            return otherTypeInfo.IsAssignableFrom(typeInfo);
        }

        // Based on: https://github.com/khellang/Scrutor/blob/master/src/Scrutor/ReflectionExtensions.cs
        private static bool IsAssignableToGenericTypeDefinition(this TypeInfo typeInfo, TypeInfo genericTypeInfo)
        {
            var interfaceTypes = typeInfo.ImplementedInterfaces.Select(t => t.GetTypeInfo());

            foreach (var interfaceType in interfaceTypes)
            {
                if (interfaceType.IsGenericType)
                {
                    var typeDefinitionTypeInfo = interfaceType
                        .GetGenericTypeDefinition()
                        .GetTypeInfo();

                    if (typeDefinitionTypeInfo.Equals(genericTypeInfo))
                    {
                        return true;
                    }
                }
            }

            if (typeInfo.IsGenericType)
            {
                var typeDefinitionTypeInfo = typeInfo
                    .GetGenericTypeDefinition()
                    .GetTypeInfo();

                if (typeDefinitionTypeInfo.Equals(genericTypeInfo))
                {
                    return true;
                }
            }

            var baseTypeInfo = typeInfo.BaseType?.GetTypeInfo();

            if (baseTypeInfo == null)
            {
                return false;
            }

            return baseTypeInfo.IsAssignableToGenericTypeDefinition(genericTypeInfo);
        }

        [Obsolete("Use GetDisplayName")]
        public static string GetFriendlyName(this Type type)
        {
            return type.GetDisplayName();
        }

        #region GetDisplayName

        private const char DefaultNestedTypeDelimiter = '+';

        private static readonly ImmutableDictionary<Type, string> _builtInTypeNames = new Dictionary<Type, string>
        {
            [typeof(void)] = "void",
            [typeof(bool)] = "bool",
            [typeof(byte)] = "byte",
            [typeof(char)] = "char",
            [typeof(decimal)] = "decimal",
            [typeof(double)] = "double",
            [typeof(float)] = "float",
            [typeof(int)] = "int",
            [typeof(long)] = "long",
            [typeof(object)] = "object",
            [typeof(sbyte)] = "sbyte",
            [typeof(short)] = "short",
            [typeof(string)] = "string",
            [typeof(uint)] = "uint",
            [typeof(ulong)] = "ulong",
            [typeof(ushort)] = "ushort"
        }.ToImmutableDictionary();

        /// <summary>
        /// Gets the display name of a type.
        /// </summary>
        /// <param name="type">The <see cref="Type"/>.</param>
        /// <param name="fullName"><c>true</c> to print a fully qualified name.</param>
        /// <param name="includeGenericParameterNames"><c>true</c> to include generic parameter names.</param>
        /// <param name="includeGenericParameters"><c>true</c> to include generic parameters.</param>
        /// <param name="nestedTypeDelimiter">Character to use as a delimiter in nested type names</param>
        /// <returns>The pretty printed type name.</returns>
        public static string GetDisplayName(
            this Type type,
            bool fullName = true,
            bool includeGenericParameterNames = false,
            bool includeGenericParameters = true,
            char nestedTypeDelimiter = DefaultNestedTypeDelimiter)
        {
            var builder = new StringBuilder();
#pragma warning disable CA1062
            ProcessType(builder, type, new DisplayNameOptions(fullName, includeGenericParameterNames, includeGenericParameters, nestedTypeDelimiter));
#pragma warning restore CA1062
            return builder.ToString();
        }

        private static void ProcessType(StringBuilder builder, Type type, in DisplayNameOptions options)
        {
            if (type.IsGenericType)
            {
                var genericArguments = type.GetGenericArguments();
                ProcessGenericType(builder, type, genericArguments, genericArguments.Length, options);
            }
            else if (type.IsArray)
            {
                ProcessArrayType(builder, type, options);
            }
            else if (_builtInTypeNames.TryGetValue(type, out var builtInName))
            {
                builder.Append(builtInName);
            }
            else if (type.IsGenericParameter)
            {
                if (options.IncludeGenericParameterNames)
                {
                    builder.Append(type.Name);
                }
            }
            else
            {
                var name = options.FullName ? type.FullName ?? type.Name : type.Name;
                builder.Append(name);

                if (options.NestedTypeDelimiter != DefaultNestedTypeDelimiter)
                {
                    builder.Replace(DefaultNestedTypeDelimiter, options.NestedTypeDelimiter, builder.Length - name.Length, name.Length);
                }
            }
        }

        private static void ProcessArrayType(StringBuilder builder, Type type, in DisplayNameOptions options)
        {
            var innerType = type;
            while (innerType!.IsArray)
            {
                innerType = innerType.GetElementType()!;
                Debug.Assert(innerType != null);
            }

            ProcessType(builder, innerType, options);

            while (type!.IsArray)
            {
                builder.Append('[');
                builder.Append(',', type.GetArrayRank() - 1);
                builder.Append(']');
                type = type.GetElementType()!;
                Debug.Assert(type != null);
            }
        }

        private static void ProcessGenericType(StringBuilder builder, Type type, Type[] genericArguments, int length, in DisplayNameOptions options)
        {
            var offset = 0;
            if (type.IsNested)
            {
                offset = type.DeclaringType!.GetGenericArguments().Length;
            }

            if (options.FullName)
            {
                if (type.IsNested)
                {
                    ProcessGenericType(builder, type.DeclaringType!, genericArguments, offset, options);
                    builder.Append(options.NestedTypeDelimiter);
                }
                else if (!string.IsNullOrEmpty(type.Namespace))
                {
                    builder.Append(type.Namespace);
                    builder.Append('.');
                }
            }

            var genericPartIndex = type.Name.IndexOf('`', StringComparison.Ordinal);
            if (genericPartIndex <= 0)
            {
                builder.Append(type.Name);
                return;
            }

            builder.Append(type.Name, 0, genericPartIndex);

            if (options.IncludeGenericParameters)
            {
                builder.Append('<');
                for (var i = offset; i < length; i++)
                {
                    ProcessType(builder, genericArguments[i], options);
                    if (i + 1 == length)
                    {
                        continue;
                    }

                    builder.Append(',');
                    if (options.IncludeGenericParameterNames || !genericArguments[i + 1].IsGenericParameter)
                    {
                        builder.Append(' ');
                    }
                }
                builder.Append('>');
            }
        }

        private readonly struct DisplayNameOptions
        {
            public DisplayNameOptions(bool fullName, bool includeGenericParameterNames, bool includeGenericParameters, char nestedTypeDelimiter)
            {
                FullName = fullName;
                IncludeGenericParameters = includeGenericParameters;
                IncludeGenericParameterNames = includeGenericParameterNames;
                NestedTypeDelimiter = nestedTypeDelimiter;
            }

            public bool FullName { get; }

            public bool IncludeGenericParameters { get; }

            public bool IncludeGenericParameterNames { get; }

            public char NestedTypeDelimiter { get; }
        }

        #endregion

        public static bool CanContainNull(this Type type)
        {
#pragma warning disable CA1062
            return !type.IsValueType
                || type.IsConstructedGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>);
#pragma warning restore CA1062
        }

        public static bool IsAssignableFromNullable(this Type type, Type nullable)
        {
            if (nullable is null)
                return false;

            var baseType = Nullable.GetUnderlyingType(nullable);

#pragma warning disable CA1062 
            return !(baseType is null) && type.IsAssignableFrom(baseType);
#pragma warning restore CA1062
        }

        // TODO: Exception type
        private static readonly Type _voidTaskResultType = Type.GetType("System.Threading.Tasks.VoidTaskResult")
            ?? throw new Exception($"Unable to reflect type 'System.Threading.Tasks.VoidTaskResult'.");

        public static Type? GetTaskResultType(this Type taskType)
        {
            if (!typeof(Task).IsAssignableFrom(taskType))
            {
                return null;
            }

#pragma warning disable CA1062
            if (!taskType.IsGenericType)
#pragma warning restore CA1062
            {
                return typeof(void);
            }

            var resultType = taskType.GetGenericArguments()[0];
            return resultType == _voidTaskResultType ? typeof(void) : resultType;
        }

        public static bool IsTaskType(this Type type, [NotNullWhen(true)] out Type? resultType)
        {
            resultType = type.GetTaskResultType();

            return resultType != null;
        }

        public static bool IsAsyncEnumerable(this Type type)
        {
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>);
        }

        public static bool IsAsyncEnumerable(this Type type, [NotNullWhen(true)] out Type? elementType)
        {
            if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(IAsyncEnumerable<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }

            elementType = null;
            return false;
        }
    }
}
