using AI4E.Messaging;

namespace AI4E.Routing
{
    public interface IRoutesResolver
    {
        bool CanResolve(DispatchDataDictionary dispatchData);

        RouteHierarchy Resolve(DispatchDataDictionary dispatchData);
    }
}
