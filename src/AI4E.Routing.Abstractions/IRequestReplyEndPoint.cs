using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;

namespace AI4E.Routing
{
    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public interface IRequestReplyEndPoint<TPacket> : IDisposable
        where TPacket : IPacket
    {
        Task<IMessage> SendAsync(TPacket packet, CancellationToken cancellation = default);
        Task<IMessageReceiveResult<TPacket>> ReceiveAsync(CancellationToken cancellation = default);
    }
}
