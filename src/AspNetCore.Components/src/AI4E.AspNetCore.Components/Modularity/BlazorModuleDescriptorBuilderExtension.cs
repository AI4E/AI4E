using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

namespace AI4E.AspNetCore.Components.Modularity
{
    public static class BlazorModuleDescriptorBuilderExtension
    {
        public static void LoadAssemblyInContext(
            this BlazorModuleDescriptor.Builder moduleBuilder,
            Assembly assembly)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var assemblyName = assembly.GetName();
#pragma warning disable CA1062
            var assemblyDescriptor = moduleBuilder.Assemblies.FirstOrDefault(
#pragma warning restore CA1062
                p => AssemblyNameComparer.BySimpleName.Equals(p.GetAssemblyName(), assemblyName));

            if (assemblyDescriptor is null)
            {
                assemblyDescriptor = BlazorModuleAssemblyDescriptor.CreateBuilder(
                    assembly, forceLoad: true);

                moduleBuilder.Assemblies.Add(assemblyDescriptor);
            }
            else
            {
                var loadAssemblySourceAsync = assemblyDescriptor.LoadAssemblySourceAsync;

                async ValueTask<BlazorModuleAssemblySource> InternalLoadAssemblySourceAsync(CancellationToken cancellation)
                {
                    var source = await loadAssemblySourceAsync(cancellation);
                    return source.Configure(forceLoad: true);
                }

                assemblyDescriptor.LoadAssemblySourceAsync = InternalLoadAssemblySourceAsync;
            }
        }
    }
}
