using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    public interface IClientEndPoint : IDisposable
    {
        ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);
        Task<IMessage> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, CancellationToken cancellation = default);
    }
}
