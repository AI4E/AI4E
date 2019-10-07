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
using static System.Diagnostics.Debug;

using System.Diagnostics.CodeAnalysis;

namespace AI4E.Utils
{
    public sealed class TypeLoadHelper
    {
        public static bool TryLoadTypeFromUnqualifiedName(string unqualifiedTypeName, [NotNullWhen(true)] out Type? type)
        {
#pragma warning disable CA1062
            if (unqualifiedTypeName.IndexOf('`', StringComparison.Ordinal) >= 0)
            {
                type = LoadGenericType(unqualifiedTypeName);
#pragma warning restore CA1062
            }
            else
            {
                type = LoadNonGenericOrTypeDefinition(unqualifiedTypeName);
            }

            return type != null;
        }

        public static Type LoadTypeFromUnqualifiedName(string unqualifiedTypeName)
        {
            if (!TryLoadTypeFromUnqualifiedName(unqualifiedTypeName, out var type))
            {
                throw new ArgumentException($"Type '{unqualifiedTypeName}' could not be loaded.");
            }

            return type!;
        }

        private static Type? LoadNonGenericOrTypeDefinition(string unqualifiedTypeName)
        {
            Assert(!unqualifiedTypeName.Contains(",", StringComparison.Ordinal));

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var type = TryLoad(assembly, unqualifiedTypeName);

                if (type != null)
                    return type;
            }

            return null;
        }

        private static Type? TryLoad(Assembly assembly, string unqualifiedTypeName)
        {
            return assembly.GetType(unqualifiedTypeName, false);
        }

        private static Type? LoadGenericType(string unqualifiedTypeName)
        {
            Type? type = null;
            var openBracketIndex = unqualifiedTypeName.IndexOf('[', StringComparison.Ordinal);
            if (openBracketIndex >= 0)
            {
                var genericTypeDefName = unqualifiedTypeName.Substring(0, openBracketIndex);
                var genericTypeDef = LoadNonGenericOrTypeDefinition(genericTypeDefName);

                if (genericTypeDef == null)
                {
                    return null;
                }

                if (genericTypeDef != null)
                {
                    var genericTypeArguments = new List<Type>();
                    var scope = 0;
                    var typeArgStartIndex = openBracketIndex + 1;
                    var endIndex = unqualifiedTypeName.Length - 1;

                    var i = openBracketIndex;
                    for (; i <= endIndex; ++i)
                    {
                        var current = unqualifiedTypeName[i];
                        switch (current)
                        {
                            case '[':
                                ++scope;
                                break;
                            case ',':
                                if (scope == 1)
                                {
                                    var typeArgName = unqualifiedTypeName.Substring(typeArgStartIndex, i - typeArgStartIndex);
                                    genericTypeArguments.Add(LoadTypeFromUnqualifiedName(typeArgName));

                                    typeArgStartIndex = i + 1;
                                }
                                break;

                            case ']':
                                --scope;
                                if (scope == 0)
                                {
                                    var typeArgName = unqualifiedTypeName.Substring(typeArgStartIndex, i - typeArgStartIndex);
                                    genericTypeArguments.Add(LoadTypeFromUnqualifiedName(typeArgName));

                                    goto X;
                                }
                                break;
                        }
                    }

X:

                    type = genericTypeDef.MakeGenericType(genericTypeArguments.ToArray());

                    // https://github.com/AI4E/AI4E.Utils/issues/50
                    if (i < endIndex)
                    {
                        if (unqualifiedTypeName[i + 1] == '[' && unqualifiedTypeName[i + 2] == ']')
                        {
                            type = type.MakeArrayType();
                        }
                        else
                        {
                            throw new NotSupportedException();
                        }
                    }
                }
            }

            return type;
        }
    }
}
