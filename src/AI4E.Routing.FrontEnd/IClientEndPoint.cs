using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.FrontEnd
{
    public interface IClientEndPoint : IDisposable
    {
        Task<IMessage> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, CancellationToken cancellation = default);
    }

    public interface IServerEndPoint : IDisposable
    {
        Task<(IMessage message, string address)> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(IMessage message, string address, CancellationToken cancellation = default);
    }
}