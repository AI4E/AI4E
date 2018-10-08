using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing
{
    public interface IMessageReceiveResult : IDisposable
    {
        CancellationToken Cancellation { get; }
        IMessage Message { get; }

        // Send the specified response and end the request.
        Task SendResponseAsync(IMessage response);
        Task SendCancellationAsync();
        Task SendAckAsync();
    }

    public interface IMessageReceiveResult<TEndPointAddress> : IMessageReceiveResult, IDisposable
    {
        TEndPointAddress RemoteEndPoint { get; }
    }

    public interface IMessageReceiveResult<TAddress, TEndPointAddress> : IMessageReceiveResult<TEndPointAddress>, IMessageReceiveResult, IDisposable
    {
        TAddress RemoteAddress { get; }
    }
}
