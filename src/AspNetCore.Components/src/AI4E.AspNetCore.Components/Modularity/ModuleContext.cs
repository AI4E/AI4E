using System;
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class ModuleContext
    {
        public ModuleContext(
            AssemblyLoadContext moduleLoadContext,
            ReflectionContext moduleReflectionContext)
        {
            if (moduleLoadContext is null)
                throw new ArgumentNullException(nameof(moduleLoadContext));

            if (moduleReflectionContext is null)
                throw new ArgumentNullException(nameof(moduleReflectionContext));

            ModuleLoadContext = moduleLoadContext;
            ModuleReflectionContext = moduleReflectionContext;
        }

        public AssemblyLoadContext ModuleLoadContext { get; }
        public ReflectionContext ModuleReflectionContext { get; }
    }
}
