using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.ApplicationParts;
using AI4E.Internal;
using AI4E.Modularity;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace AI4E.Blazor.Modularity
{
    internal sealed class InstallationSetManager : IInstallationSetManager
    {
        private static readonly string _manifestName = "blazor.app.json";

        private readonly HttpClient _httpClient;
        private readonly ApplicationPartManager _partManager;
        private readonly IModulePrefixLookup _modulePrefixLookup;
        private readonly AsyncLock _lock = new AsyncLock();

        private ISet<ModuleIdentifier> _running = new HashSet<ModuleIdentifier>();
        private IEnumerable<ModuleIdentifier> _installationSet = Enumerable.Empty<ModuleIdentifier>();
        private readonly ISet<ModuleIdentifier> _inclusiveModules = new HashSet<ModuleIdentifier>();
        private readonly ISet<ModuleIdentifier> _exclusiveModules = new HashSet<ModuleIdentifier>();

        public InstallationSetManager(HttpClient httpClient,
                                      ApplicationPartManager partManager,
                                      IModulePrefixLookup modulePrefixLookup)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            if (partManager == null)
                throw new ArgumentNullException(nameof(partManager));

            if (modulePrefixLookup == null)
                throw new ArgumentNullException(nameof(modulePrefixLookup));

            _httpClient = httpClient;
            _partManager = partManager;
            _modulePrefixLookup = modulePrefixLookup;
        }

        public event EventHandler InstallationSetChanged;

        public async Task UpdateInstallationSetAsync(IEnumerable<ModuleIdentifier> installationSet, CancellationToken cancellation)
        {
            if (installationSet == null)
                throw new ArgumentNullException(nameof(installationSet));

            if (installationSet.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default values.", nameof(installationSet));

            using (await _lock.LockAsync(cancellation))
            {
                _installationSet = installationSet;

                await UpdateAsync(cancellation);
            }
        }
        public async Task InstallAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            using (await _lock.LockAsync(cancellation))
            {
                _exclusiveModules.Remove(module);
                _inclusiveModules.Add(module);
                await UpdateAsync(cancellation);
            }
        }

        public async Task UninstallAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            using (await _lock.LockAsync(cancellation))
            {
                _inclusiveModules.Remove(module);
                _exclusiveModules.Add(module);
                await UpdateAsync(cancellation);
            }
        }

        private async Task UpdateAsync(CancellationToken cancellation)
        {
            // Build new running set
            var installationSet = _installationSet.Except(_exclusiveModules).Concat(_inclusiveModules);
            var installedModules = installationSet.Except(_running).ToList();
            var uninstalledModules = _running.Except(installationSet).ToList();

            foreach (var uninstalledModule in uninstalledModules)
            {
                await InternalUninstallAsync(uninstalledModule, cancellation);
                _running.Remove(uninstalledModule);
            }

            foreach (var installedModule in installedModules)
            {
                await InternalInstallAsync(installedModule, cancellation);
                _running.Add(installedModule);
            }
        }

        private Task InternalUninstallAsync(ModuleIdentifier moduleRelease, CancellationToken cancellation)
        {
            throw new NotSupportedException(); // TODO: We can refresh the entire page as workaround.
        }

        private async Task InternalInstallAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var manifest = await GetManifestAsync(module, cancellation);

            if (manifest == null ||
                manifest.Assemblies == null ||
                !manifest.Assemblies.Any())
            {
                return;
            }

            foreach (var assembly in manifest.Assemblies)
            {
                await InstallAssemblyAsync(assembly, cancellation);
            }
        }

        private async Task InstallAssemblyAsync(string assemblyUri, CancellationToken cancellation)
        {
            using (var assemblyStream = await _httpClient.GetStreamAsync(assemblyUri))
            using (var localAssemblyStream = await assemblyStream.ReadToMemoryAsync(cancellation))
            {
                var assemblyBytes = localAssemblyStream.ToArray();
                var assembly = Assembly.Load(assemblyBytes);
                var assemblyPart = new AssemblyPart(assembly);

                _partManager.ApplicationParts.Add(assemblyPart);
                InstallationSetChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        private async Task<BlazorModuleManifest> GetManifestAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var prefix = await GetNormalizedPrefixAsync(module, cancellation);
            var manifestUri = GetManifestUri(prefix);
            var serializer = JsonSerializer.CreateDefault();

            var response = await _httpClient.GetAsync(manifestUri, cancellation);

            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            using (var manifestStream = await response.Content.ReadAsStreamAsync())
            using (var localManifestStream = await manifestStream.ReadToMemoryAsync(cancellation))
            using (var streamReader = new StreamReader(localManifestStream))
            using (var reader = new JsonTextReader(streamReader))
            {
                return serializer.Deserialize<BlazorModuleManifest>(reader);
            }
        }

        private string GetManifestUri(string normalizedPrefix)
        {
            var manifestUri = normalizedPrefix;

            if (!manifestUri.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                manifestUri = manifestUri + "/";
            }

            manifestUri = manifestUri + _manifestName;

            return manifestUri;
        }

        private async Task<string> GetNormalizedPrefixAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            var prefix = await GetPrefixAsync(module, cancellation);

            if (!prefix.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "/" + prefix;
            }

            return prefix;
        }

        private ValueTask<string> GetPrefixAsync(ModuleIdentifier module, CancellationToken cancellation)
        {
            return _modulePrefixLookup.LookupPrefixAsync(module, cancellation);
        }

        private sealed class BlazorModuleManifest
        {
            [JsonProperty("name")]
            public string Name { get; set; }

            [JsonProperty("assemblies")]
            public List<string> Assemblies { get; set; } = new List<string>();
        }
    }
}
