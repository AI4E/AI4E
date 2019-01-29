using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Proxying;
using AI4E.Remoting;
using AI4E.Routing;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    public sealed class LogicalEndPointSkeleton : IDisposable
    {
        private readonly IEndPointManager _endPointManager;
        private readonly ILogicalEndPoint _logicalEndPoint;

        public LogicalEndPointSkeleton(IEndPointManager endPointManager, EndPointAddress endPoint)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (endPoint == default)
                throw new ArgumentDefaultException(nameof(endPoint));

            _endPointManager = endPointManager;

            _logicalEndPoint = _endPointManager.CreateLogicalEndPoint(endPoint);
        }

        public async Task<IProxy<MessageReceiveResultSkeleton>> ReceiveAsync(CancellationToken cancellation = default)
        {
            var receiveResult = await _logicalEndPoint.ReceiveAsync(cancellation);
            var receiveResultSkeleton = new MessageReceiveResultSkeleton(receiveResult);
            return new Proxy<MessageReceiveResultSkeleton>(receiveResultSkeleton, ownsInstance: true);
        }

        public async Task<byte[]> SendAsync(byte[] messageBuffer, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            var message = new Message();

            using (var stream = new MemoryStream(messageBuffer))
            {
                await message.ReadAsync(stream, cancellation);
            }

            var (response, handled) = await _logicalEndPoint.SendAsync(message, remoteEndPoint, cancellation);

            response.Trim();

            using (var stream = response.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(handled);
            }

            return response.ToArray();
        }

        public void Dispose()
        {
            _logicalEndPoint.Dispose();
        }
    }

    [Serializable]
    public struct MessageReceiveResultValues
    {
        public byte[] Message { get; set; }
        public EndPointAddress RemoteEndPoint { get; set; }

        [field: NonSerialized] // TODO: https://github.com/AI4E/AI4E/issues/62
        public CancellationToken Cancellation { get; set; }
    }

    public sealed class MessageReceiveResultSkeleton
    {
        private readonly ILogicalEndPointReceiveResult _receiveResult;

        public MessageReceiveResultSkeleton(ILogicalEndPointReceiveResult receiveResult)
        {
            if (receiveResult == null)
                throw new ArgumentNullException(nameof(receiveResult));

            _receiveResult = receiveResult;
        }

        public MessageReceiveResultValues GetResultValues()
        {
            return new MessageReceiveResultValues
            {
                Message = _receiveResult.Message.ToArray(),
                RemoteEndPoint = _receiveResult.RemoteEndPoint,
                Cancellation = _receiveResult.Cancellation
            };
        }

        public async Task SendResponseAsync(byte[] responseBuffer)
        {
            Assert(responseBuffer != null);

            var response = new Message();

            using (var stream = new MemoryStream(responseBuffer))
            {
                await response.ReadAsync(stream, cancellation: default);
            }

            await _receiveResult.SendResponseAsync(response);
        }

        public async Task SendResponseAsync(byte[] responseBuffer, bool handled)
        {
            Assert(responseBuffer != null);

            var response = new Message();

            using (var stream = new MemoryStream(responseBuffer))
            {
                await response.ReadAsync(stream, cancellation: default);
            }

            await _receiveResult.SendResponseAsync(response, handled);
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
