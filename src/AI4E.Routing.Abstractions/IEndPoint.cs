using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Routing
{
    // TODO: Move this to a helper lib, as this should not be used for public facing APIs
    public interface IEndPoint<TPacket> : IDisposable
        where TPacket : IPacket
    {
        Task<TPacket> ReceiveAsync(CancellationToken cancellation = default);
        Task SendAsync(TPacket packet, CancellationToken cancellation = default);
    }
}
