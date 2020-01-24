using System;
using System.Reflection;
using System.Runtime.Loader;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class ModuleContext
    {
        public ModuleContext(
            IBlazorModuleDescriptor moduleDescriptor,
            AssemblyLoadContext moduleLoadContext,
            ReflectionContext moduleReflectionContext,
            ITypeResolver moduleTypeResolver)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (moduleLoadContext is null)
                throw new ArgumentNullException(nameof(moduleLoadContext));

            if (moduleReflectionContext is null)
                throw new ArgumentNullException(nameof(moduleReflectionContext));

            if (moduleTypeResolver is null)
                throw new ArgumentNullException(nameof(moduleTypeResolver));

            ModuleDescriptor = moduleDescriptor;
            ModuleLoadContext = moduleLoadContext;
            ModuleReflectionContext = moduleReflectionContext;
            ModuleTypeResolver = moduleTypeResolver;
        }

        public IBlazorModuleDescriptor ModuleDescriptor { get; }
        public AssemblyLoadContext ModuleLoadContext { get; }
        public ReflectionContext ModuleReflectionContext { get; }
        public ITypeResolver ModuleTypeResolver { get; }
    }
}
