using System.Reflection;

namespace AI4E.Storage.Domain
{
    public class DomainStorageOptions
    {
        public DomainStorageOptions()
        {
            Scope = Assembly.GetEntryAssembly().GetName().Name;
        }

        public string Scope { get; set; }
        
        public int SnapshotInterval { get; set; } = 60 * 60 * 1000;

        public int SnapshotRevisionThreshold { get; set; } = 20;
    }
}
