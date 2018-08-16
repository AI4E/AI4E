using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface IServerEndPoint : IDisposable
    {
        Task<(IMessage message, string address)> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, string address, CancellationToken cancellation = default);
    }
}