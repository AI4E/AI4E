using System;
using System.Collections.Immutable;

namespace AI4E.Messaging.Routing
{
    public abstract class RouteResolver : IRouteResolver
    {
        public abstract bool TryResolve(DispatchDataDictionary dispatchData, out RouteHierarchy routes);

        public static RouteHierarchy ResolveDefaults(Type messageType)
        {
            if (messageType.IsInterface)
            {
                var route = new Route(messageType.GetUnqualifiedTypeName());

                return new RouteHierarchy(ImmutableArray.Create(route));
            }

            var result = ImmutableArray.CreateBuilder<Route>();

            for (; messageType != null; messageType = messageType.BaseType)
            {
                result.Add(new Route(messageType.GetUnqualifiedTypeName()));
            }

            return new RouteHierarchy(result.ToImmutable());
        }

        public static RouteHierarchy ResolveDefaults(DispatchDataDictionary dispatchData)
        {
            return ResolveDefaults(dispatchData.MessageType);
        }    
    }
}
