/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        RemoteEndPoint.cs 
 * Types:           AI4E.Routing.RemoteEndPoint'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.05.2018 
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
    /// <summary>
    /// Represents a remote logical end point that messages can be sent to.
    /// </summary>
    /// <typeparam name="TAddress">The type of physical address used in the protocol stack.</typeparam>
    /// <remarks>
    /// This type is not meant to be consumed directly but is part of the infrastructure to enable the remote message dispatching system.
    /// </remarks>
    public class RemoteEndPoint<TAddress> : IRemoteEndPoint<TAddress>, IAsyncDisposable
    {
        #region Fields

        private readonly IEndPointManager<TAddress> _endPointManager;
        private readonly IAsyncProvider<IPhysicalEndPoint<TAddress>> _physicalEndPointProvider;
        private readonly IMessageCoder<TAddress> _messageCoder;
        private readonly IRouteMap<TAddress> _routeManager;
        private readonly IEndPointScheduler<TAddress> _endPointScheduler;
        private readonly ILogger<RemoteEndPoint<TAddress>> _logger;

        private readonly AsyncProcess _sendProcess;
        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        // A buffer for messages to send. 
        // Messages are not sent directly to the remote end point but stored and processed one after another by a seperate async process. 
        // This enables to send again a messages that no physical end point can be found for currently or the sent failed.
        private readonly AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)> _txQueue;

        #endregion

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="RemoteEndPoint{TAddress}"/> type.
        /// </summary>
        /// <param name="endPointManager">The end-point manager, this instance is used in.</param>
        /// <param name="physicalEndPointProvider">A provider that provides a physical end point when needed.</param>
        /// <param name="route">The route of the remote virtual end point.</param>
        /// <param name="messageCoder">A message coder used to encode messages.</param>
        /// <param name="routeManager">A route map that maps virtual routes to physical addresses.</param>
        /// <param name="endPointScheduler">A scheduler that determines the order of possible replications of the remote end point.</param>
        /// <param name="logger">A logger used to log messages or null.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of 
        /// <paramref name="endPointManager"/>, 
        /// <paramref name="physicalEndPointProvider"/>, 
        /// <paramref name="route"/>, 
        /// <paramref name="messageCoder"/>, 
        /// <paramref name="routeManager"/> or 
        /// <paramref name="endPointScheduler"/> is null.
        /// </exception>
        public RemoteEndPoint(IEndPointManager<TAddress> endPointManager,
                              IAsyncProvider<IPhysicalEndPoint<TAddress>> physicalEndPointProvider,
                              EndPointRoute route,
                              IMessageCoder<TAddress> messageCoder,
                              IRouteMap<TAddress> routeManager, // TODO: Name both either route map OR route manager.
                              IEndPointScheduler<TAddress> endPointScheduler,
                              ILogger<RemoteEndPoint<TAddress>> logger)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (physicalEndPointProvider == null)
                throw new ArgumentNullException(nameof(physicalEndPointProvider));

            if (route == null)
                throw new ArgumentNullException(nameof(route));

            if (messageCoder == null)
                throw new ArgumentNullException(nameof(messageCoder));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            if (endPointScheduler == null)
                throw new ArgumentNullException(nameof(endPointScheduler));

            _endPointManager = endPointManager;
            _physicalEndPointProvider = physicalEndPointProvider;
            Route = route;
            _messageCoder = messageCoder;
            _routeManager = routeManager;
            _endPointScheduler = endPointScheduler;
            _logger = logger;

            _txQueue = new AsyncProducerConsumerQueue<(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)>();

            _sendProcess = new AsyncProcess(SendProcess);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        #endregion

        /// <summary>
        /// Gets the route of the remote virtual end point.
        /// </summary>
        public EndPointRoute Route { get; }

        /// <summary>
        /// Gets the physical address of the local physical end point.
        /// </summary>
        public TAddress LocalAddress => _endPointManager.LocalAddress;

        /// <summary>
        /// Asynchronously sends a message the replication of the remote virtual end point with the specified address.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="localEndPoint">The route of the local virtual end point.</param>
        /// <param name="remoteAddress">The physical address of the replication to send the message to.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if any of <paramref name="message"/>, <paramref name="localEndPoint"/> or <paramref name="remoteAddress"/> is null. </exception>
        /// <exception cref="ArgumentDefaultException">Thrown if <paramref name="remoteAddress"/> is the default value of type <see cref="TAddress"/>.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            if (remoteAddress == null)
                throw new ArgumentNullException(nameof(remoteAddress));

            if (remoteAddress.Equals(default(TAddress)))
                throw new ArgumentDefaultException(nameof(remoteAddress));

            await _initializationHelper.Initialization.WithCancellation(cancellation);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    await SendInternalAsync(message, localEndPoint, remoteAddress, combinedCancellation);
                }
                catch (OperationCanceledException exc) when (!cancellation.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName, exc);
                }
            }
        }

        /// <summary>
        /// Asynchronously sends a message to the remote virtual end point.
        /// </summary>
        /// <param name="message">The message to send.</param>
        /// <param name="localEndPoint">The route of the local virtual end point.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="message"/> or <paramref name="localEndPoint"/> is null. </exception>
        /// <exception cref="OperationCanceledException">Thrown if the asynchronous operation was canceled.</exception>
        public async Task SendAsync(IMessage message, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            if (message == null)
                throw new ArgumentNullException(nameof(message));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            await _initializationHelper.Initialization.WithCancellation(cancellation);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellation = _disposeHelper.CancelledOrDisposed(cancellation);

                try
                {
                    var tcs = new TaskCompletionSource<object>();

                    await _txQueue.EnqueueAsync((message, localEndPoint, attempt: 1, tcs, combinedCancellation), combinedCancellation);

                    await tcs.Task.WithCancellation(combinedCancellation);
                }
                catch (OperationCanceledException exc) when (!cancellation.IsCancellationRequested)
                {
                    throw new ObjectDisposedException(GetType().FullName, exc);
                }
            }
        }

        private IEnumerable<TAddress> Schedule(IEnumerable<TAddress> replica)
        {
            return _endPointScheduler.Schedule(replica);
        }

        #region Send process

        private async Task SendProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var (message, localEndPoint, attempt, tcs, sendCancellation) = await _txQueue.DequeueAsync(cancellation);

                    if (attempt == 1)
                    {
                        var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellation, sendCancellation);

                        sendCancellation = cts.Token;
                    }

                    Task.Run(() => SendInternalAsync(message, localEndPoint, attempt, tcs, sendCancellation)).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Remote end point {Route}: Failure on sending message to remote.");
                }
            }
        }

        private async Task Reschedule(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            // Calculate wait time in seconds
            var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

            await Task.Delay(timeToWait);

            await _txQueue.EnqueueAsync((message, localEndPoint, attempt + 1, tcs, cancellation), cancellation);
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

        private async Task SendInternalAsync(IMessage message, EndPointRoute localEndPoint, int attempt, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            try
            {
                var replica = await _routeManager.GetMapsAsync(Route, cancellation);

                replica = Schedule(replica);

                foreach (var singleReplica in replica)
                {
                    try
                    {
                        await SendAsync(message, localEndPoint, singleReplica, cancellation);
                    }
                    catch
                    {
                        continue;
                    }

                    try
                    {
                        tcs.TrySetResult(null);
                    }
                    catch (Exception exc)
                    {
                        _logger.LogWarning(exc, "Exception occured while passing a message to the remote end.");
                    }

                    return;
                }
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                try
                {
                    tcs.TrySetCanceled(cancellation);
                }
                catch (Exception exc)
                {
                    _logger.LogWarning(exc, "Exception occured while passing a message to the remote end.");
                }

                return;
            }
            catch (Exception exc)
            {
                _logger.LogWarning(exc, "Exception occured while passing a message to the remote end.");
            }

            Reschedule(message, localEndPoint, attempt, tcs, cancellation).HandleExceptions(_logger);
        }

        private async Task SendInternalAsync(IMessage message, EndPointRoute localEndPoint, TAddress remoteAddress, CancellationToken cancellation)
        {
            var frameIdx = message.FrameIndex;
            _messageCoder.EncodeMessage(message, LocalAddress, remoteAddress, Route, localEndPoint, MessageType.Message);

            try
            {
                var physicalEndPoint = await _physicalEndPointProvider.ProvideInstanceAsync(cancellation);

                Assert(physicalEndPoint != null);

                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch when (frameIdx != message.FrameIndex)
            {
                message.PopFrame();
                Assert(frameIdx == message.FrameIndex);
                throw;
            }
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _sendProcess.StartAsync(cancellation);
        }

        #endregion

        #region Disposal

        /// <summary>
        /// Gets a task that represents the disposal of the type.
        /// </summary>
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

        /// <summary>
        /// Disposes of the type.
        /// </summary>
        /// <remarks>
        /// This method does not block but instead only initiates the disposal without actually waiting till disposal is completed.
        /// </remarks>
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <summary>
        /// Asynchronously disposes of the type.
        /// </summary>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This method initiates the disposal and returns a task that represents the disposal of the type.
        /// </remarks>
        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        #endregion
    }
}
