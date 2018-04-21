/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteEndPoint.cs 
 * Types:           AI4E.Routing.RemoteEndPoint'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Routing
{
    public class RemoteEndPoint<TAddress> : IRemoteEndPoint<TAddress>, IAsyncDisposable
    {
        private readonly IEndPointManager<TAddress> _endPointManager;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly ILogger<RemoteEndPoint<TAddress>> _logger;

        private readonly AsyncProcess _sendProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        private readonly AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)> _txQueue;

        public RemoteEndPoint(IEndPointManager<TAddress> endPointManager,
                              EndPointRoute route,
                              IMessageCoder<TAddress> messageCoder,
                              IRouteMap<TAddress> routeManager,
                              ILogger<RemoteEndPoint<TAddress>> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _endPointManager = endPointManager;
            Route = route;
            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _logger = logger;

            _txQueue = new AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)>();

            _sendProcess = new AsyncProcess(SendProcess);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        public EndPointRoute Route { get; }
        public TAddress LocalAddress => _endPointManager.LocalAddress;
        public IPhysicalEndPoint<TAddress> PhysicalEndPoint => _endPointManager.PhysicalEndPoint;

        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            var frameIdx = message.FrameIndex;
            _messageCoder.EncodeMessage(message, LocalAddress, remoteAddress, Route, localEndPoint, MessageType.Message);

            try
            {
                await PhysicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();
                Assert(frameIdx == message.FrameIndex);
                throw;
            }
        }

        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            var tcs = new TaskCompletionSource<object>();

            await _txQueue.EnqueueAsync((message, localEndPoint, attempt: 0, tcs, cancellation)).WithCancellation(cancellation);

            await tcs.Task.WithCancellation(cancellation);
        }

        private IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica)
        {
            return replica; // TODO: Scheduling
        }

        #region Send process

        private async Task SendProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, localEndPoint, attempt, tcs, sendCancellation) = await _txQueue.DequeueAsync(cancellation);

                    Task.Run(() => SendInternalAsync(message, localEndPoint, attempt, tcs, cancellation, sendCancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Logging
                }
            }
        }

        private async Task Reschedule(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation, CancellationToken sendCancellation)
        {
            // Calculate wait time in seconds
            var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

            await Task.Delay(timeToWait);

            await _txQueue.EnqueueAsync((message, localEndPoint, attempt + 1, tcs, sendCancellation), cancellation);
        }

        // Adapted from: https://stackoverflow.com/questions/383587/how-do-you-do-integer-exponentiation-in-c
        private static int Pow(int x, int pow)
        {
            if (pow < 0)
                throw new ArgumentOutOfRangeException(nameof(pow));

            var result = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    result *= x;
                x *= x;
                pow >>= 1;
            }

            if (result < 0)
                return int.MaxValue;

            return result;
        }

        private async Task SendInternalAsync(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation, CancellationToken sendCancellation)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, sendCancellation);

            try
            {
                var replica = await _routeManager.GetMapsAsync(Route, cts.Token);

                replica = Schedule(replica);

                foreach (var replicat in replica)
                {
                    try
                    {
                        await SendAsync(message, localEndPoint, replicat, cts.Token);
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        tcs.SetResult(null);
                    }
                    catch (Exception exc)
                    {
                        // TODO: Logging
                    }

                    return;
                }
            }
            catch (Exception exc)
            {
                // TODO: Logging
            }

            Reschedule(message, localEndPoint, attempt, tcs, cts.Token, sendCancellation).HandleExceptions(_logger);
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _sendProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        private async Task DisposeInternalAsync()
        {
            try
            {
                await _initializationHelper.CancelAsync();
            }
            finally
            {
                await _sendProcess.TerminateAsync();
            }
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        #endregion
    }
}
