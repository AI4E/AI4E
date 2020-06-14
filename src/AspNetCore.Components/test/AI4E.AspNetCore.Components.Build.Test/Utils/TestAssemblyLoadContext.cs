using System.IO;
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.AspNetCore.Components.Build.Test.Utils
{
    public sealed class TestAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly string _assembliesPath;

        public TestAssemblyLoadContext(string assembliesPath) : base(isCollectible: true)
        {
            _assembliesPath = assembliesPath;
        }

        protected override Assembly Load(AssemblyName assemblyName)
        {
            var assemblyPath = Path.Combine(_assembliesPath, assemblyName.Name + ".dll");
            if (File.Exists(assemblyPath))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}
