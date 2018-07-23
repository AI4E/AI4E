using System;
using System.Collections.Generic;

namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleReleaseModel
    {
        public ModuleReleaseIdentifier Id { get; set; }

        public string Name { get; set; }
        public ModuleVersion Version { get; set; }
        public bool IsPreRelease => Version.IsPreRelease;

        public string Description { get; set; }
        public DateTime? ReleaseDate { get; set; }
        public string Author { get; set; }

        public List<AvailableVersionModel> AvailableVersions { get; set; } = new List<AvailableVersionModel>();
    }

    public sealed class AvailableVersionModel
    {
        public ModuleVersion Version { get; set; }
        public DateTime ReleaseDate { get; set; }
        public bool IsInstalled { get; set; }
    }
}
