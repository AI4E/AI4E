namespace AI4E.Modularity.EndPointManagement
{
    public interface IEndPointManager<TAddress> : IEndPointManager
    {
        IPhysicalEndPoint<TAddress> PhysicalEndPoint { get; }
        TAddress LocalAddress { get; }

        bool TryGetEndPoint(EndPointRoute localEndPoint, out ILocalEndPoint<TAddress> endPoint);
    }
}
