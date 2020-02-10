using System.Reflection;

namespace AI4E.Storage.Domain
{
    public class DomainStorageOptions
    {
        public DomainStorageOptions()
        {
            Scope = Assembly.GetEntryAssembly()?.GetName().Name;
        }

        public string? Scope { get; set; }   
    }
}
