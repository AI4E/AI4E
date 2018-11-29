using System;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Converters;

namespace AI4E.Blazor.Modularity
{
#if BLAZOR
    internal
#else
    public
#endif
        sealed class BlazorModuleManifest
    {
        public string Name { get; set; }

        public List<BlazorModuleManifestAssemblyEntry> Assemblies { get; set; } = new List<BlazorModuleManifestAssemblyEntry>();
    }

#if BLAZOR
    internal
#else
    public
#endif
        sealed class BlazorModuleManifestAssemblyEntry
    {
        public string AssemblyName { get; set; }

        [JsonConverter(typeof(VersionConverter))]
        public Version AssemblyVersion { get; set; }
        public bool IsAppPart { get; set; }
    }
}
