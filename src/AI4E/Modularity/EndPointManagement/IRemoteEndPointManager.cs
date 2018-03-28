namespace AI4E.Modularity.EndPointManagement
{
    public interface IRemoteEndPointManager<TAddress>
    {
        IRemoteEndPoint<TAddress> GetRemoteEndPoint(EndPointRoute remoteEndPoint);
    }
}
