using System;
using System.Collections.Immutable;

namespace AI4E.Messaging.Routing
{
    public abstract class RouteResolver : IRouteResolver
    {
        public abstract bool TryResolve(DispatchDataDictionary dispatchData, out RouteHierarchy routes);

        public static RouteHierarchy ResolveDefaults(Type messageType)
        {
            if (messageType is null)
                throw new ArgumentNullException(nameof(messageType));

            if (messageType.IsInterface)
            {
                var route = new Route(messageType);

                return new RouteHierarchy(ImmutableArray.Create(route));
            }

            var result = ImmutableArray.CreateBuilder<Route>();

            for (; messageType != null; messageType = messageType.BaseType)
            {
                result.Add(new Route(messageType));
            }

            return new RouteHierarchy(result.MoveToImmutable());
        }

        public static RouteHierarchy ResolveDefaults(DispatchDataDictionary dispatchData)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            return ResolveDefaults(dispatchData.MessageType);
        }
    }
}
