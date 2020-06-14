using System;
using System.Reflection;
using System.Runtime.Loader;

namespace AI4E.Messaging.Test
{
    internal sealed class TestAssemblyLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public TestAssemblyLoadContext() : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(Assembly.GetExecutingAssembly().Location);
        }

        public Assembly TestAssembly => LoadFromAssemblyName(Assembly.GetExecutingAssembly().GetName());

        protected override Assembly Load(AssemblyName name)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(name);
            if (assemblyPath != null && name.Name.Equals(Assembly.GetExecutingAssembly().GetName().Name, StringComparison.Ordinal))
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}
