using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.SignalR.Client
{
    public interface ISignalRClientEndPoint : IDisposable
    {
        ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);
        ValueTask<MessageSendResult> SendAsync(ValueMessage message, CancellationToken cancellation = default);
        ValueTask<MessageReceiveResult<Packet>> ReceiveAsync(CancellationToken cancellation = default);
    }
}
