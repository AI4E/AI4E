using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Modularity.EndPointManagement
{
    public interface IRemoteEndPoint : IDisposable
    {
        EndPointRoute Route { get; }

        Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation);
    }

    public interface IRemoteEndPoint<TAddress> : IRemoteEndPoint
    {
        TAddress LocalAddress { get; }

        Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation);

        Task OnRequestAsync(TAddress remoteAddress, CancellationToken cancellation);
    }
}
