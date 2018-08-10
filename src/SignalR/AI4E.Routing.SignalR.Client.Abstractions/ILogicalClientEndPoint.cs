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
    public interface ILogicalClientEndPoint
    {
        Task<IMessage> SendAsync(IMessage message, CancellationToken cancellation = default);
        Task ReceiveAsync(Func<IMessage, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation = default);

        ValueTask<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation = default);
    }
}
