using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Client
{
    public interface IRequestReplyClientEndPoint : IDisposable
    {
        Task<IMessage> SendAsync(IMessage message, CancellationToken cancellation = default);
        Task<IMessageReceiveResult> ReceiveAsync(CancellationToken cancellation = default);

        ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);
    }
}
