namespace AI4E.Modularity.Hosting.Sample.Api
{
    public sealed class ModuleSearchQuery
    {
        public ModuleSearchQuery(string searchPhrase, bool includePreReleases)
        {
            SearchPhrase = searchPhrase;
            IncludePreReleases = includePreReleases;
        }

        public string SearchPhrase { get; }
        public bool IncludePreReleases { get; }
    }
}
