using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Remoting
{
    public interface IPhysicalEndPointMultiplexer<TAddress>
    {
        IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(string address);

        Task<IPhysicalEndPoint<TAddress>> GetMultiplexEndPointAsync(string address, CancellationToken cancellation = default);

        TAddress LocalAddress { get; }
    }
}
