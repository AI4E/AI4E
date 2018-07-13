using System.Collections.Generic;

namespace AI4E.Modularity.Hosting.Sample.Models
{
    public sealed class ModulesSearchModel
    {
        public string SearchPhrase { get; set; }
        public bool IncludePreReleases { get; set; }

        public List<ModuleListModel> SearchResult { get; set; } = new List<ModuleListModel>();
    }
}
