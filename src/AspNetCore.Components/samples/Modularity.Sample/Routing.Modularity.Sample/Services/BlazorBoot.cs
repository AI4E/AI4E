using System.Collections.Generic;
using Newtonsoft.Json;

namespace Routing.Modularity.Sample.Services
{
#pragma warning disable CA1812
    internal sealed class BlazorBoot
#pragma warning restore CA1812
    {
        [JsonConstructor]
        public BlazorBoot(string main)
        {
            if (main is null)
                throw new System.ArgumentNullException(nameof(main));

            Main = main;
        }

        [JsonProperty("main")]
        public string Main { get; }

        [JsonProperty("assemblyReferences")]
        public List<string> AssemblyReferences { get; } = new List<string>();

        [JsonProperty("cssReferences")]
        public List<string> CssReferences { get; } = new List<string>();

        [JsonProperty("jsReferences")]
        public List<string> JsReferences { get;  } = new List<string>();
    }
}
