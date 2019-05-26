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
using System.Collections.Concurrent;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Utils;
using Microsoft.Extensions.Logging;

namespace AI4E.AspNetCore.Components.Modularity
{
    internal sealed class ModuleAssemblyDownloader : IModuleAssemblyDownloader
    {
        private readonly HttpClient _httpClient;
        private readonly IModulePropertiesLookup _modulePropertiesLookup;
        private readonly ILogger<ModuleAssemblyDownloader> _logger;
        private readonly ConcurrentDictionary<string, Assembly> _assemblies = new ConcurrentDictionary<string, Assembly>();

        public ModuleAssemblyDownloader(
            HttpClient httpClient,
            IModulePropertiesLookup modulePropertiesLookup,
            ILogger<ModuleAssemblyDownloader> logger = null)
        {
            if (httpClient == null)
                throw new ArgumentNullException(nameof(httpClient));

            if (modulePropertiesLookup == null)
                throw new ArgumentNullException(nameof(modulePropertiesLookup));

            _httpClient = httpClient;
            _modulePropertiesLookup = modulePropertiesLookup;
            _logger = logger;
        }

        public Assembly GetAssembly(string assemblyName)
        {
            if (!_assemblies.TryGetValue(assemblyName, out var assembly))
            {
                assembly = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(p => p.GetName().Name == assemblyName);
            }

            return assembly;
        }

        public async ValueTask<Assembly> InstallAssemblyAsync(ModuleIdentifier module, string assemblyName, CancellationToken cancellation)
        {
            var result = GetAssembly(assemblyName);
            if (result != null)
            {
                _logger?.LogDebug($"Installing assembly {assemblyName} for module {module}: Already installed.");
                return result;
            }

            _logger?.LogDebug($"Installing assembly {assemblyName} for module {module}.");

            var moduleProperties = await _modulePropertiesLookup.LookupAsync(module, cancellation);

            if (moduleProperties == null)
            {
                _logger?.LogError($"Unable to install assembly {assemblyName} for module {module}. The module properties could not be fetched.");
                return null;
            }

            foreach (var prefix in moduleProperties.Prefixes)
            {
                var assemblyUri = GetAssemblyUri(prefix, assemblyName);
                HttpResponseMessage response;

                try
                {
                    response = await _httpClient.GetAsync(assemblyUri, cancellation);
                }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Unable to load assembly {assemblyName} from source {assemblyUri}.");
                    continue;
                }

                if (response.IsSuccessStatusCode)
                {
                    var assemblyBytes = await response.Content.ReadAsByteArrayAsync();

                    try
                    {
                        result = Assembly.Load(assemblyBytes);
                    }
                    catch (Exception exc)
                    {
                        _logger?.LogWarning(exc, $"Unable to install loaded assembly {assemblyName}.");
                        continue;
                    }

                    _logger?.LogDebug($"Successfully installed assembly {assemblyName}. Response status was: {response.StatusCode} {response?.ReasonPhrase ?? string.Empty}.");

                    return result;
                }

                _logger?.LogWarning($"Unable to load assembly {assemblyName} from source {assemblyUri}.");
            }

            if (moduleProperties.Prefixes.Any())
            {
                _logger?.LogError($"Unable to load assembly {assemblyName}. No source successful.");
            }
            else
            {
                _logger?.LogError($"Unable to load assembly {assemblyName}. No sources available.");
            }

            return null;
        }

        private string GetAssemblyUri(string prefix, string assemblyName)
        {
            var assemblyUri = NormalizePrefix(prefix);

            if (!assemblyUri.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                assemblyUri = assemblyUri + "/";
            }

            assemblyUri = assemblyUri + "_framework/_bin/" + assemblyName + ".dll"; // TODO: Is this necessary? Can we avoid this?

            return assemblyUri;
        }

        private string NormalizePrefix(string prefix)
        {
            if (!prefix.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                prefix = "/" + prefix;
            }

            return prefix;
        }
    }
}
