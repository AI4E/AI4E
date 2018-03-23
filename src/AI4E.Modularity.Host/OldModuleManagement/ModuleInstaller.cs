/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        ModuleInstaller.cs
 * Types:           AI4E.Modularity.ModuleInstaller
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   22.10.2017 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Nito.AsyncEx;

// TODO: Updates
// TODO: Dependency resolution

namespace AI4E.Modularity
{
    /// <summary>
    /// Represents a module installer that is able to install modules.
    /// </summary>
    public sealed partial class ModuleInstaller : IModuleInstaller
    {
        #region Fields

        private static readonly IReadOnlyCollection<IModuleInstallation> _emtpyModuleInstallationList = new IModuleInstallation[0];
        private static readonly IReadOnlyCollection<IModuleSource> _emptyModuleSourceList = new IModuleSource[0];

        private readonly IModuleSupervision _moduleSupervision;
        private readonly IMetadataReader _metadataReader;
        private readonly Dictionary<ModuleIdentifier, ModuleInstallation> _installations = new Dictionary<ModuleIdentifier, ModuleInstallation>();
        private readonly Dictionary<string, ModuleSource> _sources = new Dictionary<string, ModuleSource>();
        private readonly AsyncLock _lock = new AsyncLock();

        private readonly string _workingDirectory = Path.Combine(".", "modules"); // TODO: This shall be configurable
        private readonly string _configFile = Path.Combine(".", "modules", "modules.lock.json");

        private readonly FileStream _configFileStream;

        private readonly Task _initialization;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="ModuleInstaller"/> type.
        /// </summary>
        /// <param name="moduleSupervision">The module supervisition that controls module runtime.</param>
        /// <param name="sourceManager">The module source manager.</param>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="moduleSupervision"/> or <paramref name="sourceManager"/> is null.</exception>
        public ModuleInstaller(IModuleSupervision moduleSupervision, IMetadataReader metadataReader)
        {
            if (moduleSupervision == null)
                throw new ArgumentNullException(nameof(moduleSupervision));

            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            if (!Directory.Exists(_workingDirectory))
            {
                Directory.CreateDirectory(_workingDirectory);
            }

            _moduleSupervision = moduleSupervision;
            _metadataReader = metadataReader;
            _configFileStream = new FileStream(_configFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None, bufferSize: 4096, useAsync: true);
            _initialization = InitializeAsync();
        }

        private async Task InitializeAsync()
        {
            var moduleConfiguration = await ReadConfigurationAsync();

            if (moduleConfiguration == null)
            {
                // Write emtpy file
                await WriteToFileAsync();

                return;
            }


            foreach (var source in moduleConfiguration.Sources)
            {
                _sources.Add(source.Name, source);
            }

            foreach (var installation in moduleConfiguration.InstalledModules)
            {
                using (var stream = new FileStream(Path.Combine(installation.InstallationDirectory, "module.json"), FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
                {
                    var metadata = await _metadataReader.ReadMetadataAsync(stream, cancellation: default);

                    installation.ModuleMetadata = metadata;

                    _installations.Add(installation.Module, installation);
                }

                await _moduleSupervision.RegisterModuleInstallationAsync(installation);
                var moduleSupervisor = _moduleSupervision.GetSupervisor(installation);
                await moduleSupervisor.StartModuleAsync();
            }
        }

        #endregion

        /// <summary>
        /// Get a collection of installed modules.
        /// </summary>
        public IReadOnlyCollection<IModuleInstallation> InstalledModules => _initialization.IsCompleted ?
                                                                            Locked(() => new List<IModuleInstallation>(_installations.Values)) :
                                                                            _emtpyModuleInstallationList;

        public IReadOnlyCollection<IModuleSource> ModuleSources => _initialization.IsCompleted ?
                                                                   Locked(() => new List<IModuleSource>(_sources.Values)) :
                                                                   _emptyModuleSourceList;

        /// <summary>
        /// Asynchronously installs the module specified by its identifier.
        /// </summary>
        /// <param name="module">The identifier of the module release.</param>
        /// <param name="source">The module source, the module shall be loaded from.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="module"/> equals <see cref="ModuleReleaseIdentifier.UnknownModuleRelease"/>.</exception>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="source"/> is null.</exception>
        /// <exception cref="ModuleInstallationException">Thrown if the specified module could not be installed.</exception>
        public async Task InstallAsync(ModuleReleaseIdentifier module, IModuleSource source)
        {
            if (module == ModuleReleaseIdentifier.UnknownModuleRelease)
                throw new ArgumentException("The module is not specified.", nameof(module));

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            await _initialization;

            var moduleSupervisor = default(IModuleSupervisor);

            using (await _lock.LockAsync())
            {
                if (_installations.TryGetValue(module.Module, out var installation))
                {
                    if (installation.Version == module.Version)
                        return;

                    // The module is already registered as installed. (This is an update)

                    throw new NotImplementedException();
                }

                var moduleLoader = GetModuleLoader(source);

                var (stream, metadata) = await moduleLoader.LoadModuleAsync(module);

                var installationDirectory = Path.Combine(_workingDirectory, module.Module.Name);

                using (var packageStream = new MemoryStream())
                {
                    await stream.CopyToAsync(packageStream, 4096);

                    using (var package = new ZipArchive(packageStream, ZipArchiveMode.Read))
                    {
                        package.ExtractToDirectory(installationDirectory);
                    }
                }

                installation = new ModuleInstallation
                {
                    Version = module.Version,
                    Module = module.Module,
                    ModuleMetadata = metadata,
                    InstallationDirectory = installationDirectory,
                    Source = (source as ModuleSource) ?? new ModuleSource(source.Name, source.Source)
                };

                _installations.Add(module.Module, installation);
                await WriteToFileAsync();

                await _moduleSupervision.RegisterModuleInstallationAsync(installation);
                moduleSupervisor = _moduleSupervision.GetSupervisor(installation);
            }

            await moduleSupervisor.StartModuleAsync();
        }

        /// <summary>
        /// Asynchronously uninstalls the module specified by its identifier.
        /// </summary>
        /// <param name="module">The module that shall be uninstalled.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentException">Thrown if <paramref name="module"/> equals <see cref="ModuleIdentifier.UnknownModule"/>.</exception>
        /// <exception cref="ModuleUninstallationException">Thrown if the module is currently installed but cannot be uninstalled.</exception>
        public async Task UninstallAsync(ModuleIdentifier module)
        {
            if (module == ModuleIdentifier.UnknownModule)
                throw new ArgumentException("The module is not specified.", nameof(module));

            await _initialization;

            using (await _lock.LockAsync())
            {
                if (!_installations.TryGetValue(module, out var installation))
                {
                    return;
                }

                _installations.Remove(module);
                await WriteToFileAsync();

                var moduleSupervisor = _moduleSupervision.GetSupervisor(installation);

                await _moduleSupervision.UnregisterModuleInstallationAsync(installation);

                await moduleSupervisor.StopModuleAsync();

                if (Directory.Exists(installation.InstallationDirectory))
                {
                    Directory.Delete(installation.InstallationDirectory);
                }
            }
        }

        public async Task AddModuleSourceAsync(string name, string source) // TODO: Validation
        {
            await _initialization;

            using (await _lock.LockAsync())
            {
                _sources.Add(name, new ModuleSource(name, source));

                await WriteToFileAsync();
            }
        }

        public async Task RemoveModuleSourceAsync(string name) // TODO: Validation
        {
            await _initialization;

            using (await _lock.LockAsync())
            {
                _sources.Remove(name);

                await WriteToFileAsync();
            }
        }

        public async Task UpdateModuleSourceAsync(string name, string source) // TODO: Validation
        {
            await _initialization;

            using (await _lock.LockAsync())
            {
                _sources.Remove(name);
                _sources.Add(name, new ModuleSource(name, source)); // TODO: When the source is not present, this shall throw

                await WriteToFileAsync();
            }
        }

        public IModuleSource GetModuleSource(string name)
        {
            if (!_initialization.IsCompleted)
                return null;

            using (_lock.Lock())
            {
                if (_sources.TryGetValue(name, out var result))
                {
                    return result;
                }

                return null;
            }
        }

        public IModuleLoader GetModuleLoader(IModuleSource moduleSource)
        {
            if (moduleSource == null)
                throw new ArgumentNullException(nameof(moduleSource));

            var uri = new Uri(moduleSource.Source);

            if (uri.IsFile)
            {
                var location = uri.LocalPath;

                return new FileSystemModuleLoader(location, _metadataReader);
            }

            throw new NotSupportedException();
        }

        private T Locked<T>(Func<T> operation)
        {
            using (_lock.Lock())
            {
                return operation();
            }
        }

        private async Task WriteToFileAsync()
        {
            var root = new ModuleConfiguration
            {
                Sources = _sources.Values.ToImmutableArray(),
                InstalledModules = _installations.Values.ToImmutableArray()
            };

            await WriteConfigurationAsync(root);
        }

        private async Task<ModuleConfiguration> ReadConfigurationAsync()
        {
            using (var memoryStream = new MemoryStream())
            {
                _configFileStream.Position = 0;
                await _configFileStream.CopyToAsync(memoryStream, bufferSize: 4096);
                memoryStream.Position = 0;

                using (var reader = new JsonTextReader(new StreamReader(memoryStream)))
                {
                    return JsonSerializer.Create().Deserialize<ModuleConfiguration>(reader);
                }
            }
        }

        private async Task WriteConfigurationAsync(ModuleConfiguration configuration)
        {
            Debug.Assert(configuration != null);

            using (var memoryStream = new MemoryStream())
            {
                using (var writer = new JsonTextWriter(new StreamWriter(memoryStream, encoding: Encoding.UTF8, bufferSize: 4096, leaveOpen: true)))
                {
                    JsonSerializer.Create().Serialize(writer, configuration, typeof(ModuleConfiguration));
                }
                memoryStream.Position = 0;
                _configFileStream.SetLength(0);
                await _configFileStream.FlushAsync();
                _configFileStream.Position = 0;

                await memoryStream.CopyToAsync(_configFileStream, bufferSize: 4096);
                await _configFileStream.FlushAsync();

            }

        }

        private sealed class ModuleInstallation : IModuleInstallation
        {
            public ModuleSource Source { get; set; }

            IModuleSource IModuleInstallation.Source => Source;

            public ModuleIdentifier Module { get; set; }

            public ModuleVersion Version { get; set; }

            public string InstallationDirectory { get; set; }

            [JsonIgnore]
            public IModuleMetadata ModuleMetadata { get; set; }
        }

        private sealed class ModuleConfiguration
        {
            [JsonProperty(PropertyName = "sources")]
            public ImmutableArray<ModuleSource> Sources { get; set; }

            [JsonProperty(PropertyName = "installed-modules")]
            public ImmutableArray<ModuleInstallation> InstalledModules { get; set; }
        }

        private sealed class ModuleSource : IModuleSource
        {
            public ModuleSource(string name, string source)
            {
                Name = name;
                Source = source;
            }

            [JsonProperty(PropertyName = "name")]
            public string Name { get; }

            [JsonProperty(PropertyName = "source")]
            public string Source { get; }
        }
    }
}
