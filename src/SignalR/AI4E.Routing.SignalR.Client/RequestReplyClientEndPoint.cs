using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;

#if BLAZOR
namespace AI4E.Routing.Blazor
#else
namespace AI4E.Routing.SignalR.Client
#endif
{
    public sealed class RequestReplyClientEndPoint : IRequestReplyClientEndPoint
    {
        private readonly IClientEndPoint _clientEndPoint;
        private readonly ILoggerFactory _loggerFactory;

        private readonly IRequestReplyEndPoint<Packet> _reqRplyEndPoint;

        public RequestReplyClientEndPoint(IClientEndPoint clientEndPoint, ILoggerFactory loggerFactory = null)
        {
            if (clientEndPoint == null)
                throw new ArgumentNullException(nameof(clientEndPoint));

            _clientEndPoint = clientEndPoint;
            _loggerFactory = loggerFactory;

            var wrapper = new ClientEndPointWrapper(clientEndPoint);
            var logger = loggerFactory?.CreateLogger<RequestReplyEndPoint<Packet>>();
            _reqRplyEndPoint = new RequestReplyEndPoint<Packet>(wrapper, logger);
        }

        public async Task<IMessage> SendAsync(IMessage message, CancellationToken cancellation = default)
        {
            return (await _reqRplyEndPoint.SendAsync(new Packet(message), cancellation)).message;
        }

        // TODO: Can we downcast here, without an async state machine? (See also: EndPointManager)
        public async Task<IMessageReceiveResult> ReceiveAsync(CancellationToken cancellation = default)
        {
            return await _reqRplyEndPoint.ReceiveAsync(cancellation);
        }

        public ValueTask<EndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return _clientEndPoint.GetLocalEndPointAsync(cancellation);
        }

        public void Dispose()
        {
            _reqRplyEndPoint.Dispose();
        }

        private readonly struct Packet : IPacket<Packet>
        {
            public Packet(IMessage message)
            {
                Message = message;
            }

            public Packet WithMessage(IMessage message)
            {
                return new Packet(message);
            }

            public IMessage Message { get; }

            IPacket IPacket.WithMessage(IMessage message)
            {
                return new Packet(message);
            }
        }

        private sealed class ClientEndPointWrapper : IEndPoint<Packet>
        {
            private readonly IClientEndPoint _clientEndPoint;

            public ClientEndPointWrapper(IClientEndPoint clientEndPoint)
            {
                _clientEndPoint = clientEndPoint;
            }

            public async Task<Packet> ReceiveAsync(CancellationToken cancellation = default)
            {
                var message = await _clientEndPoint.ReceiveAsync(cancellation);
                return new Packet(message);
            }

            public Task SendAsync(Packet packet, CancellationToken cancellation = default)
            {
                return _clientEndPoint.SendAsync(packet.Message, cancellation);
            }

            public void Dispose()
            {
                _clientEndPoint.Dispose();
            }
        }
    }
}
