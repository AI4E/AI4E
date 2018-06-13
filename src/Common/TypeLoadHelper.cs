using System;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class TypeLoadHelper
    {
        public static Type LoadTypeFromUnqualifiedName(string unqualifiedTypeName)
        {
            Assert(unqualifiedTypeName != null);

            var assemblies = AppDomain.CurrentDomain.GetAssemblies();

            foreach (var assembly in assemblies)
            {
                var t = assembly.GetType(unqualifiedTypeName, false);

                if (t != null)
                    return t;
            }

            throw new ArgumentException($"Type '{unqualifiedTypeName}' could not be loaded.");
        }
    }
}
