using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;

#nullable enable

namespace System
{
    internal static class TypeExtensions
    {
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
