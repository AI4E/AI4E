using System.Collections.Generic;
using Newtonsoft.Json;

namespace AI4E.Blazor.Modularity
{
#if BLAZOR
    internal
#else
    public
#endif
        sealed class BlazorModuleManifest
    {
        [JsonProperty("name")]
        public string Name { get; set; }

        [JsonProperty("assemblies")]
        public List<string> Assemblies { get; set; } = new List<string>();
    }
}
