using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface IServerEndPoint : IDisposable
    {
        Task<(IMessage message, EndPointAddress endPoint)> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, EndPointAddress endPoint, CancellationToken cancellation = default);
    }
}
