using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface ILogicalServerEndPoint
    {
        Task<IMessage> SendAsync(IMessage message, EndPointAddress endPoint, CancellationToken cancellation = default);
        Task ReceiveAsync(Func<IMessage, EndPointAddress, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation = default);
    }
}
