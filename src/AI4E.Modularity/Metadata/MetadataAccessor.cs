using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.Metadata
{
    public sealed class MetadataAccessor : IMetadataAccessor
    {
        private readonly IMetadataReader _metadataReader;

        public MetadataAccessor(IMetadataReader metadataReader)
        {
            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            _metadataReader = metadataReader;
        }

        public ValueTask<IModuleMetadata> GetMetadataAsync(Assembly entryAssembly, CancellationToken cancellation)
        {
            if (entryAssembly == null)
                throw new ArgumentNullException(nameof(entryAssembly));

            // First we check if there is an embedded resource
            var entryPoint = entryAssembly.EntryPoint;
            var entryPointNamespace = entryPoint.DeclaringType.Namespace;
            var metadataName = "module.json";
            var metadataFullName = $"{entryPointNamespace}.{metadataName}";
            var manifestResourceNames = entryAssembly.GetManifestResourceNames();

            if (!manifestResourceNames.Contains(metadataFullName))
            {
                metadataFullName = manifestResourceNames.FirstOrDefault(p => p.EndsWith(metadataName));
            }

            if (metadataFullName != null)
            {
                return GetMetadataFromManifestResourceAsync(entryAssembly, metadataFullName, cancellation);
            }

            // Check if there is a file in the bin directory
            var entryAssemblyLocation = entryAssembly.Location;
            var entryAssemblyDir = Path.GetDirectoryName(entryAssemblyLocation);
            var metadataPath = Path.Combine(entryAssemblyDir, metadataName);

            return GetMetadataCoreAsync(entryAssembly, metadataPath, cancellation);
        }

        private async ValueTask<IModuleMetadata> GetMetadataFromManifestResourceAsync(Assembly entryAssembly, string metadataFullName, CancellationToken cancellation)
        {
            using var manifestStream = entryAssembly.GetManifestResourceStream(metadataFullName);
            return await _metadataReader.ReadMetadataAsync(manifestStream, cancellation);
        }

        private async ValueTask<IModuleMetadata> GetMetadataCoreAsync(Assembly entryAssembly, string metadataPath, CancellationToken cancellation)
        {
            if (File.Exists(metadataPath))
            {
                try
                {
                    using var stream = new FileStream(metadataPath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true);
                    return await _metadataReader.ReadMetadataAsync(stream, cancellation);
                }
                catch (FileNotFoundException) { }
                catch (DirectoryNotFoundException) { }
            }

            // If we reach this point, we cannot find a manifest resource/file.
            // We now assembly our own metadata
            var asmName = entryAssembly.GetName();
            var asmVersion = asmName.Version;

            var module = new ModuleIdentifier(asmName.Name);
            var version = new ModuleVersion(asmVersion.Major, asmVersion.Minor, asmVersion.Revision, isPreRelease: false);

            return new ModuleMetadata(module, version);
        }
    }
}
