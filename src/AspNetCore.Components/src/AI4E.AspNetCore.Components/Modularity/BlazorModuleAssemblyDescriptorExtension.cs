using System;
using System.Reflection;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModuleAssemblyDescriptorExtension
    {
        public static IBlazorModuleAssemblyDescriptor Configure(
            this IBlazorModuleAssemblyDescriptor assemblyDescriptor,
            IBlazorModuleDescriptor? moduleDescriptor = null,
            string? assemblyName = null,
            Version? assemblyVersion = null,
            bool? isComponentAssembly = null,
            bool? forceLoad = null)
        {
            return new BlazorModuleAssemblyDescriptorWrapper(
                assemblyDescriptor,
                moduleDescriptor,
                assemblyName,
                assemblyVersion,
                isComponentAssembly,
                forceLoad);
        }

        public static AssemblyName GetAssemblyName(this IBlazorModuleAssemblyDescriptor assemblyDescriptor)
        {
#pragma warning disable CA1062
            return new AssemblyName(assemblyDescriptor.AssemblyName)
#pragma warning restore CA1062
            {
                Version = assemblyDescriptor.AssemblyVersion
            };
        }

        public static AssemblyName GetAssemblyName(this BlazorModuleAssemblyDescriptor.Builder assemblyDescriptor)
        {
#pragma warning disable CA1062
            return new AssemblyName(assemblyDescriptor.AssemblyName)
#pragma warning restore CA1062
            {
                Version = assemblyDescriptor.AssemblyVersion
            };
        }

        public static BlazorModuleAssemblyDescriptor.Builder ToBuilder(
            this IBlazorModuleAssemblyDescriptor assemblyDescriptor)
        {
            return new BlazorModuleAssemblyDescriptor.Builder(assemblyDescriptor);
        }
    }
}
