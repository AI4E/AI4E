using System;
using System.Collections.Immutable;
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
            var underlyingType = (dispatchData.Message as Validate)!.MessageType;

            if (underlyingType.IsInterface)
            {
                var route = GetRoute(underlyingType);

                return new RouteHierarchy(ImmutableArray.Create(route));
            }

            var result = ImmutableArray.CreateBuilder<Route>();

            for (; underlyingType != null; underlyingType = underlyingType.BaseType!)
            {
                result.Add(GetRoute(underlyingType));
            }

            return new RouteHierarchy(result.ToImmutable());
        }

        private static Route GetRoute(Type underlyingType)
        {
            return new Route(typeof(Validate<>).MakeGenericType(underlyingType), underlyingType);
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
