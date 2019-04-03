using System;
using System.Collections.Immutable;
using AI4E.Utils;

namespace AI4E.Routing
{
    public abstract class RoutesResolver : IRoutesResolver
    {
        public abstract bool CanResolve(DispatchDataDictionary dispatchData);

        public abstract RouteHierarchy Resolve(DispatchDataDictionary dispatchData);

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
