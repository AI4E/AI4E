using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.FrontEnd
{
    public interface ILogicalServerEndPoint
    {
        Task<IMessage> SendAsync(IMessage message, EndPointRoute endPoint, CancellationToken cancellation = default);
        Task<(IMessage message, EndPointRoute endPoint)> ReceiveAsync(Func<IMessage, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation = default);
    }
}
