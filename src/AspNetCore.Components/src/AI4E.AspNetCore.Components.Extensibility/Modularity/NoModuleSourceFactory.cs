namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class NoModuleSourceFactory : IBlazorModuleSourceFactory
    {
        public static NoModuleSourceFactory Instance { get; } = new NoModuleSourceFactory();

        private NoModuleSourceFactory() { }

        public IBlazorModuleSource CreateModuleSource()
        {
            return NoModuleSource.Instance;
        }
    }
}
