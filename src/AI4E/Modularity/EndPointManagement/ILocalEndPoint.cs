using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Modularity.EndPointManagement
{
    public interface ILocalEndPoint : IAsyncInitialization, IAsyncDisposable
    {
        EndPointRoute Route { get; }

        Task<IMessage> ReceiveAsync(CancellationToken cancellation);
        Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, CancellationToken cancellation);
    }

    public interface ILocalEndPoint<TAddress> : ILocalEndPoint
    {
        TAddress LocalAddress { get; }

        Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, TAddress remoteAddress, CancellationToken cancellation);
        Task OnReceivedAsync(IMessage message, TAddress remoteAddress, EndPointRoute remoteEndPoint, CancellationToken cancellation);
        Task OnSignalledAsync(TAddress remoteAddress, CancellationToken cancellation);
    }
}
