using System.Linq;
using AI4E.Messaging.Routing;

namespace AI4E.Messaging.Validation
{
    public sealed class ValidationRouteResolver : RouteResolver
    {
        private bool CanResolve(DispatchDataDictionary dispatchData)
        {
            return typeof(Validate).IsAssignableFrom(dispatchData.MessageType);
        }

        private RouteHierarchy Resolve(DispatchDataDictionary dispatchData)
        {
            var underlyingType = (dispatchData.Message as Validate).MessageType;

            return ResolveDefaults(underlyingType);
        }

        public override bool TryResolve(DispatchDataDictionary dispatchData, out RouteHierarchy routes)
        {
            if (!CanResolve(dispatchData))
            {
                routes = default;
                return false;
            }

            routes = Resolve(dispatchData);
            return true;
        }

        private static void Configuration(MessagingOptions options)
        {
            if (!options.RoutesResolvers.Any(p => p.GetType() == typeof(ValidationRouteResolver)))
            {
                options.RoutesResolvers.Add(new ValidationRouteResolver());
            }
        }

        public static void Register(IMessagingBuilder builder)
        {
            builder.Configure(Configuration);
        }
    }
}
