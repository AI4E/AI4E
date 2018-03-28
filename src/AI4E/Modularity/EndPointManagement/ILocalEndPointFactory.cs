namespace AI4E.Modularity.EndPointManagement
{
    public interface ILocalEndPointFactory<TAddress>
    {
        ILocalEndPoint<TAddress> CreateLocalEndPoint(IEndPointManager<TAddress> endPointManager, IRemoteEndPointManager<TAddress> remoteEndPointManager, EndPointRoute route);
    }
}
