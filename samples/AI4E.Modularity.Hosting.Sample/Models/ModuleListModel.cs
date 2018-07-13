using System;
using System.Collections.Generic;

namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleListModel
    {
        public string Id { get; set; } // TODO: Use a custom model binder instead!

        public string LatestVersion { get; set; } // TODO: Use a custom model binder instead!
        public bool IsPreRelease { get; set; }

        public List<Guid> ModuleSources { get; set; } = new List<Guid>();
    }
}
