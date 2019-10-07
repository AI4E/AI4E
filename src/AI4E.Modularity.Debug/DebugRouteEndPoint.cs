/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Messaging.Routing;
using AI4E.Utils.Async;
using AI4E.Utils.Messaging.Primitives;
using AI4E.Utils.Proxying;
using Microsoft.Extensions.Logging;

namespace AI4E.Modularity.Debug
{
    internal sealed class DebugRouteEndPoint : IRouteEndPoint
    {
        private readonly IProxy<RouteEndPointSkeleton> _proxy;
        private readonly ILogger<DebugRouteEndPoint> _logger;

        public DebugRouteEndPoint(IProxy<RouteEndPointSkeleton> proxy, ILogger<DebugRouteEndPoint> logger = null)
        {
            if (proxy is null)
                throw new ArgumentNullException(nameof(proxy));

            _proxy = proxy;
            _logger = logger;
        }

        public RouteEndPointAddress EndPoint { get; }

        public async ValueTask<IRouteEndPointReceiveResult> ReceiveAsync(CancellationToken cancellation = default)
        {
            var proxy = _proxy;

            var resultProxy = await proxy.ExecuteAsync(p => p.ReceiveAsync(cancellation));
            var resultValues = await resultProxy.ExecuteAsync(p => p.GetResultValues());

            return new DebugMessageReceiveResult(resultProxy, resultValues);
        }

        public async ValueTask<RouteMessageHandleResult> SendAsync(ValueMessage message, RouteEndPointAddress remoteEndPoint, CancellationToken cancellation = default)
        {
            var proxy = _proxy;
            var buffer = new byte[message.Length];
            ValueMessage.WriteToMemory(message, buffer.AsSpan());
            var responseBuffer = await proxy.ExecuteAsync(p => p.SendAsync(buffer, remoteEndPoint, cancellation));
            var response = ValueMessage.ReadFromMemory(responseBuffer);

            response = response.PopFrame(out var frame);

            bool handled;

            using (var frameStream = frame.OpenStream())
            using (var reader = new BinaryReader(frameStream))
            {
                handled = reader.ReadBoolean();
            }

            return new RouteMessageHandleResult(new RouteMessage<IDispatchResult>(response), handled);
        }

        #region Disposal

        public ValueTask DisposeAsync()
        {
            return _proxy.DisposeAsync();
        }

        #endregion

        private sealed class DebugMessageReceiveResult : IRouteEndPointReceiveResult
        {
            private readonly IProxy<MessageReceiveResultSkeleton> _proxy;
            private readonly MessageReceiveResultValues _values;

            public DebugMessageReceiveResult(IProxy<MessageReceiveResultSkeleton> proxy, MessageReceiveResultValues values)
            {
                _proxy = proxy;
                _values = values;
            }

            public RouteEndPointAddress RemoteEndPoint => _values.RemoteEndPoint;

            public CancellationToken Cancellation => _values.Cancellation;

            public ValueMessage Message => ValueMessage.ReadFromMemory(_values.Message.AsSpan());

            public async ValueTask SendResultAsync(RouteMessageHandleResult result)
            {
                var message = result.RouteMessage.Message;
                var responseBuffer = new byte[message.Length];
                ValueMessage.WriteToMemory(message, responseBuffer.AsSpan());
                await _proxy.ExecuteAsync(p => p.SendResponseAsync(responseBuffer, result.Handled));
            }

            public async ValueTask SendCancellationAsync()
            {
                await _proxy.ExecuteAsync(p => p.SendCancellationAsync());
            }

            public async ValueTask SendAckAsync()
            {
                await _proxy.ExecuteAsync(p => p.SendAckAsync());
            }

            public void Dispose()
            {
                _proxy.ExecuteAsync(p => p.Dispose()).HandleExceptions();
            }
        }

        internal sealed class RouteEndPointSkeleton : IDisposable
        {
            private readonly AsyncDisposeHelper _disposeHelper;
            private readonly IRouteEndPoint _routeEndPoint;

            public RouteEndPointSkeleton(IRouteEndPoint routeEndPoint)
            {
                if (routeEndPoint is null)
                    throw new ArgumentNullException(nameof(routeEndPoint));

                _disposeHelper = new AsyncDisposeHelper(_routeEndPoint.DisposeAsync);
                _routeEndPoint = routeEndPoint;
            }

            public async Task<IProxy<MessageReceiveResultSkeleton>> ReceiveAsync(CancellationToken cancellation = default)
            {
                var endPoint = _routeEndPoint;
                var receiveResult = await endPoint.ReceiveAsync(cancellation);
                var receiveResultSkeleton = new MessageReceiveResultSkeleton(receiveResult);
                return ProxyHost.CreateProxy(receiveResultSkeleton, ownsInstance: true);
            }

            public async Task<byte[]> SendAsync(byte[] messageBuffer, RouteEndPointAddress remoteEndPoint, CancellationToken cancellation = default)
            {
                var endPoint = _routeEndPoint;
                var message = ValueMessage.ReadFromMemory(messageBuffer);
                var sendResult = await endPoint.SendAsync(message, remoteEndPoint, cancellation);

                var response = sendResult.RouteMessage.Message;
                var frameBuilder = new ValueMessageFrameBuilder();

                using (var frameStream = frameBuilder.OpenStream())
                using (var writer = new BinaryWriter(frameStream))
                {
                    writer.Write(sendResult.Handled);
                }

                response = response.PushFrame(frameBuilder.BuildMessageFrame());

                var result = new byte[response.Length];
                ValueMessage.WriteToMemory(response, result);
                return result;
            }

            public void Dispose()
            {
                _disposeHelper.Dispose();
            }
        }

        [Serializable]
        internal struct MessageReceiveResultValues
        {
            public byte[] Message { get; set; }
            public RouteEndPointAddress RemoteEndPoint { get; set; }

            [field: NonSerialized] // TODO: https://github.com/AI4E/AI4E/issues/62
            public CancellationToken Cancellation { get; set; }
        }

        internal sealed class MessageReceiveResultSkeleton
        {
            private readonly IRouteEndPointReceiveResult _receiveResult;

            public MessageReceiveResultSkeleton(IRouteEndPointReceiveResult receiveResult)
            {
                if (receiveResult == null)
                    throw new ArgumentNullException(nameof(receiveResult));

                _receiveResult = receiveResult;
            }

            public MessageReceiveResultValues GetResultValues()
            {
                var messageBytes = new byte[_receiveResult.Message.Length];
                ValueMessage.WriteToMemory(_receiveResult.Message, messageBytes.AsSpan());

                return new MessageReceiveResultValues
                {
                    Message = messageBytes,
                    RemoteEndPoint = _receiveResult.RemoteEndPoint,
                    Cancellation = _receiveResult.Cancellation
                };
            }

            public async Task SendResponseAsync(byte[] responseBuffer, bool handled)
            {
                System.Diagnostics.Debug.Assert(responseBuffer != null);

                var response = ValueMessage.ReadFromMemory(responseBuffer.AsSpan());

                await _receiveResult.SendResultAsync(
                    new RouteMessageHandleResult(
                        new RouteMessage<IDispatchResult>(response), handled)).AsTask();
            }

            public Task SendCancellationAsync()
            {
                return _receiveResult.SendCancellationAsync().AsTask();
            }

            public Task SendAckAsync()
            {
                return _receiveResult.SendAckAsync().AsTask();
            }

            public void Dispose() { }
        }
    }
}
