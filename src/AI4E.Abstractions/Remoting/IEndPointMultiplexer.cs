using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Remoting
{
    public interface IEndPointMultiplexer<TAddress>
    {
        IPhysicalEndPoint<TAddress> GetMultiplexEndPoint(string address);

        Task<IPhysicalEndPoint<TAddress>> GetMultiplexEndPointAsync(string address, CancellationToken cancellation = default);

        TAddress LocalAddress { get; }
    }
}
