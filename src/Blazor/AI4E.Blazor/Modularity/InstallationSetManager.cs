using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Blazor.ApplicationParts;
using AI4E.Internal;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using Newtonsoft.Json;
using Nito.AsyncEx;

namespace AI4E.Blazor.Modularity
{
    public sealed class InstallationSetManager : IInstallationSetManager
    {
        private static readonly string _manifestName = "blazor.app.json";

        private readonly HttpClient _httpClient;
        private readonly ApplicationPartManager _partManager;
        private readonly IModulePrefixLookup _modulePrefixLookup;
        private readonly AsyncLock _lock = new AsyncLock();
        private ResolvedInstallationSet _installationSet;

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

        public async Task UpdateInstallationSetAsync(ResolvedInstallationSet installationSet, CancellationToken cancellation)
        {
            using (await _lock.LockAsync(cancellation))
            {
                var oldInstallationSet = _installationSet;
                var installedModules = installationSet.Resolved.Except(oldInstallationSet.Resolved);
                var uninstalledModules = oldInstallationSet.Resolved.Except(installationSet.Resolved);

                foreach (var uninstalledModule in uninstalledModules)
                {
                    await UninstallAsync(uninstalledModule, cancellation);
                }

                foreach (var installedModule in installedModules)
                {
                    await InstallAsync(installedModule, cancellation);
                }

                _installationSet = installationSet;
            }
        }

        private Task UninstallAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            throw new NotSupportedException(); // TODO: We can refresh the entire page as workaround.
        }

        private async Task InstallAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var manifest = await GetManifestAsync(moduleRelease, cancellation);

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
            }
        }

        private async Task<BlazorModuleManifest> GetManifestAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var prefix = await GetNormalizedPrefixAsync(moduleRelease, cancellation);
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

        private async Task<string> GetNormalizedPrefixAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            var prefix = await GetPrefixAsync(moduleRelease, cancellation);

            if (!prefix.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "/" + prefix;
            }

            return prefix;
        }

        private ValueTask<string> GetPrefixAsync(ModuleReleaseIdentifier moduleRelease, CancellationToken cancellation)
        {
            return _modulePrefixLookup.LookupPrefixAsync(moduleRelease.Module, cancellation);

            //return Task.FromResult("/module"); // TODO: Implement prefix lookup
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
