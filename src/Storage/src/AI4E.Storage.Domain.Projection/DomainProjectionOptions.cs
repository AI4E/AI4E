using System.Collections.Generic;
using System.Reflection;

namespace AI4E.Storage.Domain.Projection
{
    public class DomainProjectionOptions
    {
        public bool IncludeAssemblyDependencies { get; set; } = true;
        public List<AssemblyName> ExcludedAssemblies { get; } = new List<AssemblyName>();
    }
}
