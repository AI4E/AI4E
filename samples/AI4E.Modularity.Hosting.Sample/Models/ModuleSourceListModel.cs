using System;

namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModuleSourceListModel
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string Location { get; set; }
        public bool IsLocalSource { get; set; }
    }
}
