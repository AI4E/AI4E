using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Routing;

namespace AI4E.Modularity.Host
{
    public interface IModulePropertiesLookup
    {
        ValueTask<ModuleProperties> LookupAsync(ModuleIdentifier module, CancellationToken cancellation);
    }

    public static class IModulePropertiesLookupExtension
    {
        public static async ValueTask<string> LookupPrefixAsync(
            this IModulePropertiesLookup propertiesLookup,
            ModuleIdentifier module,
            CancellationToken cancellation)
        {
            var props = await propertiesLookup.LookupAsync(module, cancellation);
            return props?.Prefixes.FirstOrDefault();
        }
    }
}
