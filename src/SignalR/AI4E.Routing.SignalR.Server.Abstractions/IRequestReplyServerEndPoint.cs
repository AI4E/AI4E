using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing.SignalR.Server
{
    public interface IRequestReplyServerEndPoint : IDisposable
    {
        Task<IRequestReplyServerReceiveResult> ReceiveAsync(CancellationToken cancellation = default);
        Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default);
    }

    public interface IRequestReplyServerReceiveResult : IMessageReceiveResult<Packet<EndPointAddress>>
    {
        EndPointAddress RemoteEndPoint { get; }
    }
}
