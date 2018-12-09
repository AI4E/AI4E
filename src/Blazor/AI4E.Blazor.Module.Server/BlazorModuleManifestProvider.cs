using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.Modularity;
using AI4E.Modularity;

namespace AI4E.Blazor.Module.Server
{
    public sealed class BlazorModuleManifestProvider : IBlazorModuleManifestProvider
    {
        private readonly Assembly _appAssembly;
        private readonly IMetadataAccessor _metadataAccessor;

        public BlazorModuleManifestProvider(Assembly appAssembly, IMetadataAccessor metadataAccessor)
        {
            if (appAssembly == null)
                throw new ArgumentNullException(nameof(appAssembly));

            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            _appAssembly = appAssembly;
            _metadataAccessor = metadataAccessor;
        }

        public async ValueTask<BlazorModuleManifest> GetBlazorModuleManifestAsync(CancellationToken cancellation)
        {
            return new BlazorModuleManifest
            {
                Name = (await _metadataAccessor.GetMetadataAsync(cancellation)).Name,
                Assemblies = GetAppAssemblies()
            };
        }

        private List<BlazorModuleManifestAssemblyEntry> GetAppAssemblies()
        {
            var assemblies = new Dictionary<string, Assembly>();
            var blazorConfig = BlazorConfig.Read(_appAssembly.Location);

            AddAssemblyAndDependencies(_appAssembly, assemblies);

            var result = new List<BlazorModuleManifestAssemblyEntry>(capacity: assemblies.Count);
            var sourceOutputDir = Path.GetDirectoryName(blazorConfig.SourceOutputAssemblyPath);

            foreach (var assembly in assemblies.Values)
            {
                var dllFile = Path.Combine(sourceOutputDir, Path.GetFileName(assembly.Location));

                if (File.Exists(dllFile))
                {
                    var dllFileRef = AssemblyName.GetAssemblyName(dllFile);

                    result.Add(new BlazorModuleManifestAssemblyEntry
                    {
                        AssemblyName = assembly.GetName().Name,
                        AssemblyVersion = dllFileRef.Version,
                        IsAppPart = assembly == _appAssembly
                    });
                }
            }

            return result;

        }

        private static void AddAssemblyAndDependencies(Assembly asm, Dictionary<string, Assembly> assemblies)
        {
            var asmName = asm.GetName().Name;

            if (assemblies.ContainsKey(asmName))
            {
                return;
            }

            assemblies.Add(asm.GetName().Name, asm);

            foreach (var dependencyRef in asm.GetReferencedAssemblies())
            {
                var dependency = Assembly.Load(dependencyRef);
                AddAssemblyAndDependencies(dependency, assemblies);
            }
        }
    }
}
