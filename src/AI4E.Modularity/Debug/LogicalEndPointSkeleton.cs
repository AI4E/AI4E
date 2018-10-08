using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Remoting;
using AI4E.Routing;

namespace AI4E.Modularity.Debug
{
    public sealed class LogicalEndPointSkeleton : IAsyncDisposable
    {
        private readonly IEndPointManager _endPointManager;
        private readonly ILogicalEndPoint _logicalEndPoint;

        public LogicalEndPointSkeleton(IEndPointManager endPointManager, EndPointAddress endPoint)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPointManager = endPointManager;

            _logicalEndPoint = _endPointManager.CreateLogicalEndPoint(endPoint);
        }

        public async Task<byte[]> ReceiveAsync(CancellationToken cancellation = default)
        {
            var message = await _logicalEndPoint.ReceiveAsync(cancellation);

            var buffer = new byte[message.Length];

            using (var stream = new MemoryStream(buffer, writable: true))
            {
                await message.WriteAsync(stream, cancellation);
            }

            return buffer;
        }

        public async Task SendAsync(byte[] messageBuffer, EndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            var message = new Message();

            using (var stream = new MemoryStream(messageBuffer))
            {
                await message.ReadAsync(stream, cancellation);
            }

            await _logicalEndPoint.SendAsync(message, remoteEndPoint, cancellation);
        }

        public async Task SendAsync(byte[] responseBuffer, byte[] requestBuffer, CancellationToken cancellation = default)
        {
            var response = new Message();
            var request = new Message();

            using (var stream = new MemoryStream(responseBuffer))
            {
                await response.ReadAsync(stream, cancellation);
            }

            using (var stream = new MemoryStream(requestBuffer))
            {
                await request.ReadAsync(stream, cancellation);
            }

            await _logicalEndPoint.SendAsync(response, request, cancellation);
        }

        #region Disposal

        public Task Disposal => _logicalEndPoint.Disposal;

        public void Dispose()
        {
            _logicalEndPoint.Dispose();
        }

        public Task DisposeAsync()
        {
            return _logicalEndPoint.DisposeAsync();
        }

        #endregion
    }
}
