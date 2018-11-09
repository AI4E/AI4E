using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing.SignalR.Server
{
    public sealed class RequestReplyServerEndPoint : IRequestReplyServerEndPoint
    {
        private readonly IServerEndPoint _serverEndPoint;
        private readonly ILoggerFactory _loggerFactory;

        private readonly IRequestReplyEndPoint<Packet<EndPointAddress>> _reqRplyEndPoint;

        public RequestReplyServerEndPoint(IServerEndPoint serverEndPoint, ILoggerFactory loggerFactory = null)
        {
            if (serverEndPoint == null)
                throw new ArgumentNullException(nameof(serverEndPoint));

            _serverEndPoint = serverEndPoint;
            _loggerFactory = loggerFactory;

            var wrapper = new ServerEndPointWrapper(serverEndPoint);
            var logger = loggerFactory?.CreateLogger<RequestReplyEndPoint<Packet<EndPointAddress>>>();
            _reqRplyEndPoint = new RequestReplyEndPoint<Packet<EndPointAddress>>(wrapper, logger);
        }

        public async Task<IRequestReplyServerReceiveResult> ReceiveAsync(CancellationToken cancellation = default)
        {
            var receiveResult = await _reqRplyEndPoint.ReceiveAsync(cancellation);
            return new RequestReplyServerReceiveResult(receiveResult);
        }

        public Task<IMessage> SendAsync(IMessage message, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            return _reqRplyEndPoint.SendAsync(new Packet<EndPointAddress>(message, remoteEndPoint), cancellation);
        }

        public void Dispose()
        {
            _reqRplyEndPoint.Dispose();
        }

        private sealed class ServerEndPointWrapper : IEndPoint<Packet<EndPointAddress>>
        {
            private readonly IServerEndPoint _serverEndPoint;

            public ServerEndPointWrapper(IServerEndPoint serverEndPoint)
            {
                _serverEndPoint = serverEndPoint;
            }

            public async Task<Packet<EndPointAddress>> ReceiveAsync(CancellationToken cancellation = default)
            {
                var (message, endPoint) = await _serverEndPoint.ReceiveAsync(cancellation);

                return new Packet<EndPointAddress>(message, endPoint);
            }

            public Task SendAsync(Packet<EndPointAddress> packet, CancellationToken cancellation = default)
            {
                return _serverEndPoint.SendAsync(packet.Message, packet.EndPoint, cancellation);
            }

            public void Dispose()
            {
                _serverEndPoint.Dispose();
            }
        }

        private sealed class RequestReplyServerReceiveResult : IRequestReplyServerReceiveResult
        {
            private readonly IMessageReceiveResult<Packet<EndPointAddress>> _receiveResult;

            public RequestReplyServerReceiveResult(IMessageReceiveResult<Packet<EndPointAddress>> receiveResult)
            {
                _receiveResult = receiveResult;
            }

            public EndPointAddress RemoteEndPoint => _receiveResult.Packet.EndPoint;

            public Packet<EndPointAddress> Packet => _receiveResult.Packet;

            public CancellationToken Cancellation => _receiveResult.Cancellation;

            public IMessage Message => _receiveResult.Message;

            public Task SendResponseAsync(IMessage response)
            {
                return _receiveResult.SendResponseAsync(response);
            }

            public Task SendCancellationAsync()
            {
                return _receiveResult.SendCancellationAsync();
            }

            public Task SendAckAsync()
            {
                return _receiveResult.SendAckAsync();
            }

            public void Dispose()
            {
                _receiveResult.Dispose();
            }
        }
    }
}
