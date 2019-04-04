using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Client
{
    public interface IClientEndPoint : IDisposable
    {
        ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);
        Task<IMessage> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, CancellationToken cancellation = default);
    }
}
