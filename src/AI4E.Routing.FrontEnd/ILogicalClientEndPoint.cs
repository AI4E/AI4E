using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.FrontEnd
{
    public interface ILogicalClientEndPoint
    {
        Task<IMessage> SendAsync(IMessage message, CancellationToken cancellation = default);
        Task ReceiveAsync(Func<IMessage, CancellationToken, Task<IMessage>> handler, CancellationToken cancellation = default);

        ValueTask<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation = default);
    }
}
