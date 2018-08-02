using System;
using System.Collections.Concurrent;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;

namespace AI4E.Routing.FrontEnd
{
    public sealed class LogicalClientEndPoint
    {
        private readonly IClientEndPoint _endPoint;
        private readonly ILogger<LogicalClientEndPoint> _logger;
        private readonly AsyncInitializationHelper<(EndPointRoute localEndPoint, string securityToken)> _initializationHelper;

        private int _nextSeqNum = 0;
        private readonly ConcurrentDictionary<int, Func<IMessage, Stream, Task>> _responseTable = new ConcurrentDictionary<int, Func<IMessage, Stream, Task>>();

        public LogicalClientEndPoint(IClientEndPoint endPoint, ILogger<LogicalClientEndPoint> logger = null)
        {
            if (endPoint == null)
                throw new ArgumentNullException(nameof(endPoint));

            _endPoint = endPoint;
            _logger = logger;
            _initializationHelper = new AsyncInitializationHelper<(EndPointRoute localEndPoint, string securityToken)>(InitializeInternalAsync);
        }

        private async Task<(EndPointRoute localEndPoint, string securityToken)> InitializeInternalAsync(CancellationToken cancellation)
        {
            var taskCompletionSource = new TaskCompletionSource<(EndPointRoute localEndPoint, string securityToken)>();

            Task HandleInitResponseAsync(IMessage message, Stream stream)
            {
                using (var reader = new BinaryReader(stream))
                {
                    var endPointBytesLength = reader.ReadInt32();
                    var endPointBytes = reader.ReadBytes(endPointBytesLength);
                    var endPoint = Encoding.UTF8.GetString(endPointBytes);

                    var securityTokenBytesLength = reader.ReadInt32();
                    var securityTokenBytes = reader.ReadBytes(securityTokenBytesLength);
                    var securityToken = Encoding.UTF8.GetString(securityTokenBytes);

                    taskCompletionSource.SetResult((EndPointRoute.CreateRoute(endPoint), securityToken));
                }

                return Task.CompletedTask;
            }

            var seqNum = GetNextSeqNum();

            while (!_responseTable.TryAdd(seqNum, HandleInitResponseAsync))
            {
                seqNum = GetNextSeqNum();
            }

            var initRequest = new Message();

            using (var stream = initRequest.PushFrame().OpenStream())
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(seqNum);
                writer.Write((int)MessageType.Init);
            }

            await _endPoint.SendAsync(initRequest, cancellation);
            return await taskCompletionSource.Task.WithCancellation(cancellation);
        }

        public async Task<EndPointRoute> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            var (localEndPoint, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return localEndPoint;
        }

        public async Task SendAsync(IMessage message, CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);


        }

        public async Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            await _initializationHelper.Initialization.WithCancellation(cancellation);

            return await _endPoint.ReceiveAsync(cancellation);
        }

        private int GetNextSeqNum()
        {
            return Interlocked.Increment(ref _nextSeqNum);
        }

        private enum MessageType : int
        {
            Unknown = 0,
            Init = 1,
            InitAck = 2,
            InitReject = 3,
            Close = 4,
            CloseAck = 5,
            Message = 6
        }
    }
}
