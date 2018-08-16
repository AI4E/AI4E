using System;
using System.Collections.Generic;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class TypeLoadHelper
    {
        public static Type LoadTypeFromUnqualifiedName(string unqualifiedTypeName)
        {
            if (unqualifiedTypeName.IndexOf('`') >= 0)
            {
                var type = LoadGenericType(unqualifiedTypeName);

                Console.WriteLine($"Resolving '{unqualifiedTypeName}' as '{type.FullName}'");

                return type;
            }
            else
            {
                return LoadNonGenericOrTypeDefinition(unqualifiedTypeName);
            }
        }

        private static Type LoadNonGenericOrTypeDefinition(string unqualifiedTypeName)
        {
            Assert(!unqualifiedTypeName.Contains(","));

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var type = TryLoad(assembly, unqualifiedTypeName);

                if (type != null)
                    return type;
            }

            throw new ArgumentException($"Type '{unqualifiedTypeName}' could not be loaded.");
        }

        private static Type TryLoad(Assembly assembly, string unqualifiedTypeName)
        {
            return assembly.GetType(unqualifiedTypeName, false);
        }

        private static Type LoadGenericType(string unqualifiedTypeName)
        {
            Type type = null;
            var openBracketIndex = unqualifiedTypeName.IndexOf('[');
            if (openBracketIndex >= 0)
            {
                var genericTypeDefName = unqualifiedTypeName.Substring(0, openBracketIndex);
                var genericTypeDef = LoadNonGenericOrTypeDefinition(genericTypeDefName);
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

                    // TODO
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
