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

// TODO: Do we need to use an HttpClient, when running server-side. 
//       We could request the assemblies from the modules directly.

using System;
using System.Buffers;
using System.Net.Http;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleAssemblyLoader : IBlazorModuleAssemblyLoader
    {
        private readonly HttpClient _httpClient;
        private readonly BlazorModuleOptions _options;
        private readonly ILogger<BlazorModuleAssemblyLoader>? _logger;

        public BlazorModuleAssemblyLoader(
            HttpClient httpClient,
            IOptions<BlazorModuleOptions> optionsProvider,
            ILogger<BlazorModuleAssemblyLoader>? logger)
        {
            if (httpClient is null)
                throw new ArgumentNullException(nameof(httpClient));

            if (optionsProvider is null)
                throw new ArgumentNullException(nameof(optionsProvider));

            _httpClient = httpClient;
            _options = optionsProvider.Value ?? new BlazorModuleOptions();
            _logger = logger;
        }

        public async ValueTask<BlazorModuleAssemblySource> LoadAssemblySourceAsync(
            BlazorModuleDescriptor moduleDescriptor,
            AssemblyName assemblyName,
            CancellationToken cancellation = default)
        {
            if (moduleDescriptor is null)
                throw new ArgumentNullException(nameof(moduleDescriptor));

            if (assemblyName is null)
                throw new ArgumentNullException(nameof(assemblyName));

            if (assemblyName.Name is null)
                throw new ArgumentException("An assembly name must be specified.", nameof(assemblyName));

            var assemblyBytesOwner = await LoadAssemblyBytesAsync(moduleDescriptor, assemblyName, cancellation)
                .ConfigureAwait(false);

            try
            {
                if (!_options.LoadSymbols)
                {
                    return new BlazorModuleAssemblySource(assemblyBytesOwner);
                }

                var (assemblySymbolsBytesOwner, symbolsLoaded) =
                    await LoadAssemblySymbolsBytesAsync(moduleDescriptor, assemblyName, cancellation)
                    .ConfigureAwait(false);

                if (symbolsLoaded)
                {
                    return new BlazorModuleAssemblySource(assemblyBytesOwner, assemblySymbolsBytesOwner);
                }

                return new BlazorModuleAssemblySource(assemblyBytesOwner);
            }
            catch
            {
                assemblyBytesOwner.Dispose();

                throw;
            }
        }

        private async ValueTask<(SlicedMemoryOwner<byte> bytesOwner, bool success)> LoadResourceAsync(
            string uri,
            CancellationToken cancellation)
        {
            using var response = await GetAsync(uri, cancellation)
                .ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
                return (default, false);

            var contentLength = response.Content.Headers.ContentLength;
            using var stream = new PooledMemoryStream(unchecked((int)(contentLength ?? 0)), MemoryPool<byte>.Shared);
            try
            {
                await response.Content.CopyToAsync(stream).ConfigureAwait(false);
            }
            catch
            {
                stream.MemoryOwner.Dispose();
                throw;
            }

            return (stream.MemoryOwner, true);
        }

        private ValueTask<(SlicedMemoryOwner<byte> bytesOwner, bool success)> LoadAssemblySymbolsBytesAsync(
            BlazorModuleDescriptor moduleDescriptor,
            AssemblyName assemblyName,
            CancellationToken cancellation)
        {
            var uri = GetAssemblyUri(moduleDescriptor.UrlPrefix, assemblyName.Name!, extension: ".pdb");
            return LoadResourceAsync(uri, cancellation);
        }

        private async ValueTask<SlicedMemoryOwner<byte>> LoadAssemblyBytesAsync(
            BlazorModuleDescriptor moduleDescriptor,
            AssemblyName assemblyName,
            CancellationToken cancellation)
        {
            var assemblyUri = GetAssemblyUri(moduleDescriptor.UrlPrefix, assemblyName.Name!);
            var (result, success) = await LoadResourceAsync(assemblyUri, cancellation)
                .ConfigureAwait(false);

            if (!success)
            {
                _logger?.LogError($"Unable to load assembly {assemblyName.Name} from source {assemblyUri}.");
                throw new BlazorModuleAssemblyLoadException(); // TODO: can we tell anything about the reason?
            }

            _logger?.LogDebug($"Successfully loaded assembly {assemblyName.Name}.");

            return result;
        }

        private async Task<HttpResponseMessage> GetAsync(string resourceUri, CancellationToken cancellation)
        {
            try
            {
                return await _httpClient.GetAsync(resourceUri, cancellation)
                    .ConfigureAwait(false);
            }
            catch (HttpRequestException exc)
            {
                _logger?.LogWarning(exc, $"Unable to load resource {resourceUri}.");

                throw new BlazorModuleAssemblyLoadException("Unable to load module assembly.", exc);
            }
        }

        private string GetAssemblyUri(string prefix, string assemblyName, string extension = ".dll")
        {
            var assemblyUriBuilder = new StringBuilder();
            var baseAddress = _httpClient.BaseAddress.ToString();

            assemblyUriBuilder.Append(baseAddress);

            if (!baseAddress.EndsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                assemblyUriBuilder.Append('/');
            }

            assemblyUriBuilder.Append(prefix);

            if (!prefix.StartsWith("/", StringComparison.OrdinalIgnoreCase))
            {
                assemblyUriBuilder.Append('/');
            }

            assemblyUriBuilder.Append("_framework/_bin/"); // TODO: Shouldn't this be part of the prefix already?
            assemblyUriBuilder.Append(Uri.EscapeDataString(assemblyName));
            assemblyUriBuilder.Append(extension);

            return assemblyUriBuilder.ToString();
        }

        public void Dispose() { }
    }
}
