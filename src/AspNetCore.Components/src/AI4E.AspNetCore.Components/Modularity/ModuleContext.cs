using System;
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class ModuleContext
    {
        public ModuleContext(
            IBlazorModuleDescriptor moduleDescriptor,
            AssemblyLoadContext moduleLoadContext,
            ReflectionContext moduleReflectionContext)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (moduleLoadContext is null)
                throw new ArgumentNullException(nameof(moduleLoadContext));

            if (moduleReflectionContext is null)
                throw new ArgumentNullException(nameof(moduleReflectionContext));

            ModuleDescriptor = moduleDescriptor;
            ModuleLoadContext = moduleLoadContext;
            ModuleReflectionContext = moduleReflectionContext;
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }
        public AssemblyLoadContext ModuleLoadContext { get; }
        public ReflectionContext ModuleReflectionContext { get; }
    }
}
