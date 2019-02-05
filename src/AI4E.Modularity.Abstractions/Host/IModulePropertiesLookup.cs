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

        public static async ValueTask<EndPointAddress?> LookupEndPointAsync(
            this IModulePropertiesLookup propertiesLookup,
            ModuleIdentifier module,
            CancellationToken cancellation)
        {
            var props = await propertiesLookup.LookupAsync(module, cancellation);

            if (props == null || props.EndPoints.Count == 0)
                return null;

            return props.EndPoints[0];
        }
    }
}
