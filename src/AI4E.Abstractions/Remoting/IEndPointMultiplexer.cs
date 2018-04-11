namespace AI4E.Remoting
{
    public interface IEndPointMultiplexer<TAddress>
    {
        IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(string address);
    }
}
