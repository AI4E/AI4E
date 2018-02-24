using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Modularity
{
    public sealed partial class FileSystemModuleLoader : IModuleLoader
    {
        private readonly DirectoryInfo _directory;

        public FileSystemModuleLoader(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            _directory = new DirectoryInfo(path);
        }

        public async Task<IEnumerable<ModuleReleaseIdentifier>> ListModulesAsync()
        {
            if (!_directory.Exists)
                return Enumerable.Empty<ModuleReleaseIdentifier>();

            var files = _directory.GetFiles("*.aep", SearchOption.AllDirectories);
            var result = new List<IModuleMetadata>();

            foreach (var file in files)
            {
                var metadata = await ReadMetadataAsync(file);

                if (metadata != null)
                {
                    result.Add(metadata);
                }
            }

            return result.Select(p => new ModuleReleaseIdentifier(new ModuleIdentifier(p.Name), p.Version));
        }

        public async Task<IModuleMetadata> LoadModuleMetadataAsync(ModuleReleaseIdentifier identifier)
        {
            if (!_directory.Exists)
                return null;

            var hints = _directory.GetFiles($"{identifier}.aep", SearchOption.AllDirectories);

            foreach (var hint in hints)
            {
                var metadata = await ReadMetadataAsync(hint);

                if (metadata != null && new ModuleReleaseIdentifier(metadata.Name, metadata.Version) == identifier)
                {
                    return metadata;
                }
            }

            var files = _directory.GetFiles("*.aep", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var metadata = await ReadMetadataAsync(file);

                if (metadata != null && new ModuleReleaseIdentifier(metadata.Name, metadata.Version) == identifier)
                {
                    return metadata;
                }
            }

            return null;
        }

        private async Task<IModuleMetadata> ReadMetadataAsync(FileInfo file)
        {
            if (!file.Exists)
                return null;

            using (var fileStream = file.OpenReadAsync())
            using (var package = new ZipArchive(fileStream, ZipArchiveMode.Read))
            {
                var manifest = package.GetEntry("module.json");

                // Invalid package
                if (manifest == null)
                {
                    return null;
                }

                return await ModuleMetadataReader.ReadAsync(manifest.Open());
            }
        }

        public async Task<(Stream, IModuleMetadata)> LoadModuleAsync(ModuleReleaseIdentifier identifier)
        {
            if (!_directory.Exists)
                return default;

            var hints = _directory.GetFiles($"{identifier}.aep", SearchOption.AllDirectories);

            foreach (var hint in hints)
            {
                var metadata = await ReadMetadataAsync(hint);

                if (metadata != null && new ModuleReleaseIdentifier(metadata.Name, metadata.Version) == identifier)
                {
                    return (hint.OpenReadAsync(), metadata);
                }
            }

            var files = _directory.GetFiles("*.aep", SearchOption.AllDirectories);

            foreach (var file in files)
            {
                var metadata = await ReadMetadataAsync(file);

                if (metadata != null && new ModuleReleaseIdentifier(metadata.Name, metadata.Version) == identifier)
                {
                    return (file.OpenReadAsync(), metadata);
                }
            }

            return default;
        }


    }
}
