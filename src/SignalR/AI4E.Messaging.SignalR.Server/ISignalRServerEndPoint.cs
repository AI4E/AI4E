using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Messaging.Primitives;

namespace AI4E.Messaging.SignalR.Server
{
    public interface ISignalRServerEndPoint : IDisposable
    {
        ValueTask<MessageReceiveResult<SignalRServerPacket>> ReceiveAsync(CancellationToken cancellation = default);

        ValueTask<MessageSendResult> SendAsync(
            SignalRServerPacket packet,
            CancellationToken cancellation = default);
    }

    public readonly struct SignalRServerPacket : IPacket<SignalRServerPacket>
    {
        public SignalRServerPacket(in ValueMessage message, in RouteEndPointAddress remoteEndPoint)
        {
            Message = message;
            RemoteEndPoint = remoteEndPoint;
        }

        public ValueMessage Message { get; }
        public RouteEndPointAddress RemoteEndPoint { get; }

        public SignalRServerPacket WithMessage(in ValueMessage message)
        {
            return new SignalRServerPacket(message, RemoteEndPoint);
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in ValueMessage message)
        {
            return WithMessage(message);
        }
#endif
    }
}
