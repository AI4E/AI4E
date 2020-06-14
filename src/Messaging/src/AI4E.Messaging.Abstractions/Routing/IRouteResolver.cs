namespace AI4E.Messaging.Routing
{
    public interface IRouteResolver
    {
        bool TryResolve(DispatchDataDictionary dispatchData, out RouteHierarchy routes);
    }
}
