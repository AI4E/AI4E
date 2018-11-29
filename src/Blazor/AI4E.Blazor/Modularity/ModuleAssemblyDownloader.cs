using System;
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Modularity;

namespace AI4E.Blazor.Modularity
{
    internal sealed class ModuleAssemblyDownloader : IModuleAssemblyDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly IModulePrefixLookup _modulePrefixLookup;

        private readonly ConcurrentDictionary<string, Assembly> _assemblies = new ConcurrentDictionary<string, Assembly>();

        public ModuleAssemblyDownloader(HttpClient httpClient, IModulePrefixLookup modulePrefixLookup)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            if (modulePrefixLookup == null)
                throw new ArgumentNullException(nameof(modulePrefixLookup));

            _httpClient = httpClient;
            _modulePrefixLookup = modulePrefixLookup;
        }

        public Assembly GetAssembly(string assemblyName)
        {
            if (!_assemblies.TryGetValue(assemblyName, out var assembly))
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(p => p.GetName().Name == assemblyName);
            }

            return assembly;
        }

        public async Task InstallAssemblyAsync(ModuleIdentifier module, string assemblyName, CancellationToken cancellation)
        {
            Console.WriteLine("Loading assembly: " + assemblyName);

            var assemblyUri = GetAssemblyUri(await GetNormalizedPrefixAsync(module, cancellation), assemblyName);

            using (var assemblyStream = await _httpClient.GetStreamAsync(assemblyUri))
            using (var localAssemblyStream = await assemblyStream.ReadToMemoryAsync(cancellation))
            {
                var assemblyBytes = localAssemblyStream.ToArray();
                var assembly = Assembly.Load(assemblyBytes);
            }
        }

        private string GetAssemblyUri(string normalizedPrefix, string assemblyName)
        {
            var assemblyUri = normalizedPrefix;

            if (!assemblyUri.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                assemblyUri = assemblyUri + "/";
            }

            assemblyUri = assemblyUri + assemblyName + ".dll"; // TODO: Is this necessary? Can we avoid this?

            return assemblyUri;
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
    }
}
