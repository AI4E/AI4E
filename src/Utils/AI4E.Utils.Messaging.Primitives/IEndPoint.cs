using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Messaging.Primitives
{
    public interface IEndPoint<TPacket> : IDisposable
        where TPacket : IPacket
    {
        ValueTask<TPacket> ReceiveAsync(CancellationToken cancellation = default);
        ValueTask SendAsync(TPacket packet, CancellationToken cancellation = default);
    }

    public interface IEndPoint : IEndPoint<Packet>
    {
        new ValueTask<ValueMessage> ReceiveAsync(CancellationToken cancellation = default);
        ValueTask SendAsync(ValueMessage message, CancellationToken cancellation = default);

#if SUPPORTS_DEFAULT_INTERFACE_METHODS
        async ValueTask<Packet> IEndPoint<Packet>.ReceiveAsync(CancellationToken cancellation)
        {
            return new Packet(await ReceiveAsync(cancellation));
        }

        ValueTask IEndPoint<Packet>.SendAsync(Packet packet, CancellationToken cancellation)
        {
            return SendAsync(packet.Message, cancellation);
        }
#endif
    }
}
