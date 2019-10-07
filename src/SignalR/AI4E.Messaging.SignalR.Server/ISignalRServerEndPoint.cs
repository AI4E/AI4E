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
        public SignalRServerPacket(in Message message, in RouteEndPointAddress remoteEndPoint)
        {
            Message = message;
            RemoteEndPoint = remoteEndPoint;
        }

        public Message Message { get; }
        public RouteEndPointAddress RemoteEndPoint { get; }

        public SignalRServerPacket WithMessage(in Message message)
        {
            return new SignalRServerPacket(message, RemoteEndPoint);
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        IPacket IPacket.WithMessage(in Message message)
        {
            return WithMessage(message);
        }
#endif
    }
}
