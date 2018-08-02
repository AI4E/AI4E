using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Internal;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class FileSystemModuleSource : AggregateRoot, IModuleSource
    {
        private ModuleSourceName _name;
        private FileSystemModuleSourceLocation _location;

        // The cache's key type should be ModuleReleaseIdentifier actually but JSON.NET is unable to deserialize it.
        [JsonProperty("Cache")]
        private readonly Dictionary<string, CacheEntry> _cache = new Dictionary<string, CacheEntry>();

        public FileSystemModuleSource(Guid id, ModuleSourceName name, FileSystemModuleSourceLocation location) : base(id)
        {
            if (name == default)
                throw new ArgumentDefaultException(nameof(name));

            if (location == default)
                throw new ArgumentDefaultException(nameof(location));

            _name = name;
            _location = location;

            Notify(new ModuleSourceAdded(id, location));
        }

        public ModuleSourceName Name
        {
            get => _name;
            set
            {
                if (value == _name)
                    return;

                if (value == default)
                    throw new ArgumentDefaultException(nameof(value));

                _name = value;
            }
        }

        public FileSystemModuleSourceLocation Location
        {
            get => _location;
            set
            {
                if (value == _location)
                    return;

                if (value == null)
                    throw new ArgumentNullException(nameof(value));

                _location = value;
                _cache.Clear();

                Notify(new FileSystemModuleSourceLocationChanged(Id, value));
            }
        }

        public async Task<IEnumerable<ModuleReleaseIdentifier>> GetAvailableAsync(string searchPhrase,
                                                                                  bool includePreReleases,
                                                                                  IMetadataReader moduleMetadataReader,
                                                                                  CancellationToken cancellation)
        {
            if (moduleMetadataReader == null)
                throw new ArgumentNullException(nameof(moduleMetadataReader));

            var regex = searchPhrase != null ? new Regex(searchPhrase, RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled) : null;

            if (!Directory.Exists(_location.Location))
            {
                return Enumerable.Empty<ModuleReleaseIdentifier>();
            }

            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(_location.Location, "*.aep", SearchOption.AllDirectories);
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return Enumerable.Empty<ModuleReleaseIdentifier>();
            }

            var result = new List<ModuleReleaseIdentifier>();

            foreach (var file in files)
            {
                var metadata = await ReadMetadataAsync(file, moduleMetadataReader, cancellation);

                if (metadata == null)
                {
                    continue;
                }

                UpdateCacheEntry(metadata.Release, file, metadata);

                if ((regex == null || regex.IsMatch(metadata.Name)) &&
                    (includePreReleases || !metadata.Version.IsPreRelease))
                {
                    result.Add(metadata.Release);
                }
            }

            return result;
        }

        // TODO: Add a type to manage module packages.
        private async Task<IModuleMetadata> ReadMetadataAsync(string file, IMetadataReader moduleMetadataReader, CancellationToken cancellation)
        {
            var path = file;

            if (!File.Exists(path))
                return null;

            try
            {
                using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                using (var package = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    var manifest = package.GetEntry("module.json");

                    // Invalid package
                    if (manifest == null)
                    {
                        // TODO: Log
                        return null;
                    }

                    try
                    {
                        return await moduleMetadataReader.ReadMetadataAsync(manifest.Open(), cancellation);
                    }
                    catch (ModuleMetadataFormatException)
                    {
                        // TODO: Log
                        return null;
                    }
                }
            }
            catch (FileNotFoundException) // The file was deleted concurrently.
            {
                return null;
            }
            catch (IOException)
            {
                // TODO: Log

                return null;
            }
        }

        public async ValueTask<IModuleMetadata> GetMetadataAsync(ModuleReleaseIdentifier module,
                                                                 IMetadataReader moduleMetadataReader,
                                                                 CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (moduleMetadataReader == null)
                throw new ArgumentNullException(nameof(moduleMetadataReader));

            if (TryGetCacheEntry(module, out var cacheEntry))
            {
                return cacheEntry.Metadata;
            }

            if (!Directory.Exists(_location.Location))
                return null;

            // If we cannot find the module in the cache, chances are good that the aep source file's name is equal to the module name.
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(_location.Location, $"{module.Module}*.aep", SearchOption.AllDirectories);

                if (await GetMatchingMetadata(module, files, moduleMetadataReader, cancellation) is var matching &&
                    matching.metadata != null)
                {
                    return matching.metadata;
                }
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return null;
            }



            // We have to search for the module be opening all files (except the ones, we already looked at.
            try
            {
                var checkedFiles = files.ToArray();

                files = Directory.EnumerateFiles(_location.Location, $"*.aep", SearchOption.AllDirectories)
                                  .Except(checkedFiles);

                return (await GetMatchingMetadata(module, files, moduleMetadataReader, cancellation)).metadata;
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return null;
            }
        }

        private async Task<(IModuleMetadata metadata, string file)> GetMatchingMetadata(ModuleReleaseIdentifier module,
                                                                                        IEnumerable<string> files,
                                                                                        IMetadataReader moduleMetadataReader,
                                                                                        CancellationToken cancellation)
        {
            foreach (var file in files)
            {
                var metadata = await ReadMetadataAsync(file, moduleMetadataReader, cancellation);

                if (metadata == null)
                {
                    continue;
                }

                UpdateCacheEntry(metadata.Release, file, metadata);


                if (metadata != null &&
                    metadata.Release == module)
                {
                    return (metadata, file);
                }
            }

            return (null, null);
        }

        public async ValueTask<DirectoryInfo> ExtractAsync(DirectoryInfo directory,
                                                           ModuleReleaseIdentifier module,
                                                           IMetadataReader moduleMetadataReader,
                                                           CancellationToken cancellation)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (TryGetCacheEntry(module, out var cacheEntry))
            {
                return await ExtractCoreAsync(directory, cacheEntry.Metadata, cacheEntry.Path, cancellation);
            }

            if (!Directory.Exists(_location.Location))
                return null;

            // If we cannot find the module in the cache, chances are good that the aep source file's name is equal to the module name.
            IEnumerable<string> hints;

            try
            {
                hints = Directory.GetFiles(_location.Location, $"{module.Module}*.aep", SearchOption.AllDirectories);

                if (await GetMatchingMetadata(module, hints, moduleMetadataReader, cancellation) is var matching &&
                    matching.metadata != null)
                {
                    return await ExtractCoreAsync(directory, matching.metadata, matching.file, cancellation);
                }
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return null;
            }

            // We have to search for the module be opening all files (except the ones, we already looked at.
            try
            {
                var files = Directory.GetFiles(_location.Location, $"*.aep", SearchOption.AllDirectories)
                                     .Except(hints);

                if (await GetMatchingMetadata(module, files, moduleMetadataReader, cancellation) is var matching &&
                                matching.metadata != null)
                {
                    return await ExtractCoreAsync(directory, matching.metadata, matching.file, cancellation);
                }

                return null;
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return null;
            }
        }

        // TODO: Add a type to manage module packages.
        private async Task<DirectoryInfo> ExtractCoreAsync(DirectoryInfo directory, IModuleMetadata metadata, string file, CancellationToken cancellation)
        {
            if (file == null)
            {
                return null;
            }

            var path = file;

            if (!File.Exists(path))
                return null;

            var moduleDirectory = Path.Combine(directory.FullName, metadata.Release.ToString());

            using (var memoryStream = new MemoryStream())
            {
                try
                {
                    // Copy package to memory.
                    using (var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                    {
                        await fileStream.CopyToAsync(memoryStream, bufferSize: 4096);
                    }
                }
                catch (FileNotFoundException) // The file was deleted concurrently.
                {
                    return null;
                }
                catch (IOException)
                {
                    // TODO: Log
                    return null;
                }

                do
                {
                    memoryStream.Position = 0;

                    if (!Directory.Exists(moduleDirectory))
                    {
                        Directory.CreateDirectory(moduleDirectory);
                    }

                    try
                    {
                        using (var package = new ZipArchive(memoryStream, ZipArchiveMode.Read))
                        {
                            await package.ExtractToDirectoryAsync(moduleDirectory, overwrite: true, cancellation);
                        }
                    }
                    catch (DirectoryNotFoundException)
                    {
                        continue;
                    }

                    return new DirectoryInfo(moduleDirectory);

                }
                while (true);
            }
        }

        protected override void DoDispose()
        {
            Notify(new ModuleSourceRemoved(Id));
        }

        private bool TryGetCacheEntry(ModuleReleaseIdentifier release, out CacheEntry cacheEntry)
        {
            Assert(release != default);

            return _cache.TryGetValue(release.ToString(), out cacheEntry);
        }

        private CacheEntry UpdateCacheEntry(ModuleReleaseIdentifier release, string path, IModuleMetadata metadata)
        {
            Assert(release != default);

            if (!_cache.TryGetValue(release.ToString(), out var cacheEntry))
            {
                cacheEntry = new CacheEntry(/*release*/);

                _cache.Add(release.ToString(), cacheEntry);
            }

            cacheEntry.Path = path;
            cacheEntry.Metadata = metadata;

            return cacheEntry;
        }

        private sealed class CacheEntry
        {
            //public CacheEntry(ModuleReleaseIdentifier release)
            //{
            //    Assert(release != default);

            //    Release = release;
            //}

            //public ModuleReleaseIdentifier Release { get; }
            public string Path { get; set; }
            public IModuleMetadata Metadata { get; set; }
        }
    }
}
