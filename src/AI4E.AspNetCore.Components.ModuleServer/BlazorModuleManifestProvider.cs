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
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.AspNetCore.Components.Modularity;
using AI4E.Modularity.Metadata;
using Newtonsoft.Json;

namespace AI4E.AspNetCore.Components.ModuleServer
{
    public sealed class BlazorModuleManifestProvider : IBlazorModuleManifestProvider
    {
        private readonly Assembly _appAssembly;
        private readonly IMetadataAccessor _metadataAccessor;

        public BlazorModuleManifestProvider(Assembly appAssembly, IMetadataAccessor metadataAccessor)
        {
            if (appAssembly == null)
                throw new ArgumentNullException(nameof(appAssembly));

            if (metadataAccessor == null)
                throw new ArgumentNullException(nameof(metadataAccessor));

            _appAssembly = appAssembly;
            _metadataAccessor = metadataAccessor;
        }

        public async ValueTask<BlazorModuleManifest> GetBlazorModuleManifestAsync(CancellationToken cancellation)
        {
            return new BlazorModuleManifest
            {
                Name = (await _metadataAccessor.GetMetadataAsync(cancellation)).Name,
                Assemblies = GetAppAssemblies()
            };
        }

        private List<BlazorModuleManifestAssemblyEntry> GetAppAssemblies()
        {
            var blazorConfig = BlazorConfig.Read(_appAssembly.Location);
            var distPath = blazorConfig.DistPath;
            var blazorBootPath = Path.Combine(distPath, "_framework", "blazor.boot.json");

            BlazorBoot blazorBoot;

            using (var fileStream = new FileStream(blazorBootPath, FileMode.Open))
            using (var streamReader = new StreamReader(fileStream))
            {
                blazorBoot = (BlazorBoot)JsonSerializer.CreateDefault().Deserialize(streamReader, typeof(BlazorBoot));
            }

            var binPath = Path.Combine(distPath, "_framework", "_bin");

            var result = new List<BlazorModuleManifestAssemblyEntry>(capacity: blazorBoot.AssemblyReferences.Count + 1);

            foreach (var assembly in blazorBoot.AssemblyReferences.Where(p => p.EndsWith(".dll", StringComparison.OrdinalIgnoreCase)).Append(blazorBoot.Main))
            {
                var dllFile = Path.Combine(binPath, assembly);

                if (File.Exists(dllFile))
                {
                    var dllFileRef = AssemblyName.GetAssemblyName(dllFile);

                    result.Add(new BlazorModuleManifestAssemblyEntry
                    {
                        AssemblyName = dllFileRef.Name,
                        AssemblyVersion = dllFileRef.Version,
                        IsComponentAssembly = assembly == blazorBoot.Main
                    });
                }
            }

            return result;

        }

        private sealed class BlazorBoot
        {
            [JsonProperty("main")]
            public string Main { get; set; }

            [JsonProperty("assemblyReferences")]
            public List<string> AssemblyReferences { get; set; } = new List<string>();

            [JsonProperty("cssReferences")]
            public List<string> CssReferences { get; set; } = new List<string>();

            [JsonProperty("jsReferences")]
            public List<string> JsReferences { get; set; } = new List<string>();
        }
    }
}
