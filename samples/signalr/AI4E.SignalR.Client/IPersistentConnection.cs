using System;
using System.Threading.Tasks;
using AI4E.Remoting;
using System.Threading;

namespace AI4E.SignalR.Client
{
    public interface IPersistentConnection : IDisposable
    {
        Task<IMessage> ReceiveAsync(CancellationToken cancellation);
        Task SendAsync(CancellationToken cancellation);
    }
}
