/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Domain;
using AI4E.Utils;
using Newtonsoft.Json;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    public sealed class FileSystemModuleSource : AggregateRoot, IModuleSource
    {
        private ModuleSourceName _name;
        private FileSystemModuleSourceLocation _location;

        long IModuleSource.Revision
        {
            get => Revision;
            set => Revision = value;
        }

        string IModuleSource.ConcurrencyToken
        {
            get => ConcurrencyToken;
            set => ConcurrencyToken = value;
        }

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

        Guid IModuleSource.Id => Id;

        public async Task<IEnumerable<ModuleReleaseIdentifier>> GetAvailableAsync(string searchPhrase,
                                                                                  bool includePreReleases,
                                                                                  IMetadataReader moduleMetadataReader,
                                                                                  CancellationToken cancellation)
        {
            if (moduleMetadataReader == null)
                throw new ArgumentNullException(nameof(moduleMetadataReader));

            var regex = ProcessSearchPhrase(searchPhrase);

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

        private static Regex ProcessSearchPhrase(string searchPhrase)
        {
            if (searchPhrase == null)
                return null;

            var parts = searchPhrase.Split(default(char[]), StringSplitOptions.RemoveEmptyEntries);

            if (!parts.Any())
                return null;

            Assert(!parts.Any(p => p.ContainsWhitespace()));

            string PreprocessSearchPhrasePart(string part)
            {
                var resultBuilder = new StringBuilder();

                for (var i = 0; i < part.Length; i++)
                {
                    switch (part[i])
                    {
                        case '*':
                            resultBuilder.Append('.');
                            resultBuilder.Append('*');
                            break;

                        case '+':
                            resultBuilder.Append('.');
                            break;

                        // Unescape everything
                        case '.':
                        case '\\':
                        case '?':
                        case '^':
                        case '|':
                        case '{':
                        case '}':
                        case '[':
                        case ']':
                        case '<':
                        case '>':
                        case ':':
                            resultBuilder.Append('\\');
                            resultBuilder.Append(part[i]);
                            break;

                        default:
                            resultBuilder.Append(part[i]);
                            break;
                    }
                }

                return resultBuilder.ToString();
            }

            StringBuilder AppendPattern(StringBuilder regexBuilder, string subPattern)
            {
                if (regexBuilder.Length != 0)
                {
                    regexBuilder.Append('|');
                }

                regexBuilder.Append('(');
                regexBuilder.Append('?');
                regexBuilder.Append(':');
                regexBuilder.Append(subPattern);
                regexBuilder.Append(')');

                return regexBuilder;
            }

            var pattern = parts.Select(PreprocessSearchPhrasePart).Aggregate(new StringBuilder(), AppendPattern).ToString();

            return new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant | RegexOptions.Compiled | RegexOptions.IgnoreCase);
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

        public async ValueTask<IModuleMetadata> GetMetadataAsync(ModuleIdentifier module,
                                                                 ModuleVersion version,
                                                                 IMetadataReader moduleMetadataReader,
                                                                 CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            if (version == default)
                throw new ArgumentDefaultException(nameof(version));

            var moduleRelease = new ModuleReleaseIdentifier(module, version);

            if (moduleMetadataReader == null)
                throw new ArgumentNullException(nameof(moduleMetadataReader));

            if (TryGetCacheEntry(moduleRelease, out var cacheEntry))
            {
                return cacheEntry.Metadata;
            }

            if (!Directory.Exists(_location.Location))
                return null;

            // If we cannot find the module in the cache, chances are good that the aep source file's name is equal to the module name.
            IEnumerable<string> files;

            try
            {
                files = Directory.EnumerateFiles(_location.Location, $"{module}*.aep", SearchOption.AllDirectories);

                if (await GetMatchingMetadata(moduleRelease, files, moduleMetadataReader, cancellation) is var matching &&
                    matching.metadata != null)
                {
                    return matching.metadata;
                }
            }
            catch (DirectoryNotFoundException) // The directory was deleted concurrently.
            {
                return null;
            }

            // We have to search for the module by opening all files (except the ones, we already looked at.
            try
            {
                var checkedFiles = files.ToArray();

                files = Directory.EnumerateFiles(_location.Location, "*.aep", SearchOption.AllDirectories)
                                  .Except(checkedFiles);

                return (await GetMatchingMetadata(moduleRelease, files, moduleMetadataReader, cancellation)).metadata;
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

            // We have to search for the module by opening all files, except the ones, we already inspected.
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

            if (Directory.Exists(moduleDirectory))
            {
                Directory.Delete(moduleDirectory);
            }

            ZipFile.ExtractToDirectory(file, moduleDirectory);

            return new DirectoryInfo(moduleDirectory);
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

    internal static partial class PathInternal
    {
        /// <summary>Returns a comparison that can be used to compare file and directory names for equality.</summary>
        internal static StringComparison StringComparison => IsCaseSensitive ?
                                                             StringComparison.Ordinal :
                                                             StringComparison.OrdinalIgnoreCase;

        /// <summary>Gets whether the system is case-sensitive.</summary>
        internal static bool IsCaseSensitive { get; } = GetIsCaseSensitive();

        /// <summary>
        /// Determines whether the file system is case sensitive.
        /// </summary>
        /// <remarks>
        /// Ideally we'd use something like pathconf with _PC_CASE_SENSITIVE, but that is non-portable, 
        /// not supported on Windows or Linux, etc. For now, this function creates a tmp file with capital letters 
        /// and then tests for its existence with lower-case letters.  This could return invalid results in corner 
        /// cases where, for example, different file systems are mounted with differing sensitivities.
        /// </remarks>
        private static bool GetIsCaseSensitive()
        {
            try
            {
                var pathWithUpperCase = Path.Combine(Path.GetTempPath(), "CASESENSITIVETEST" + Guid.NewGuid().ToString("N"));
                using (new FileStream(pathWithUpperCase, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None, 0x1000, FileOptions.DeleteOnClose))
                {
                    var lowerCased = pathWithUpperCase.ToLowerInvariant();
                    return !File.Exists(lowerCased);
                }
            }
            catch (Exception exc)
            {
                // In case something goes terribly wrong, we don't want to fail just because
                // of a casing test, so we assume case-insensitive-but-preserving.
                System.Diagnostics.Debug.Fail("Casing test failed: " + exc);
                return false;
            }
        }
    }
}
