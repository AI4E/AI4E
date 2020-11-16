/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;

namespace AI4E.Messaging.Routing
{
    public sealed class MessageRouter : IMessageRouter
    {
        private readonly IRouteMessageHandler _routeMessageHandler;
        private readonly IRouteEndPoint _routeEndPoint;
        private readonly IRouteManager _routeManager;
        private readonly ILogger<MessageRouter>? _logger;

        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncDisposeHelper _disposeHelper;

        public MessageRouter(
            IRouteMessageHandler routeMessageHandler,
            IRouteEndPoint routeEndPoint,
            IRouteManager routeManager,
            ILogger<MessageRouter>? logger = null)
        {
            if (routeMessageHandler == null)
                throw new ArgumentNullException(nameof(routeMessageHandler));

            if (routeEndPoint == null)
                throw new ArgumentNullException(nameof(routeEndPoint));

            if (routeManager == null)
                throw new ArgumentNullException(nameof(routeManager));

            _routeMessageHandler = routeMessageHandler;
            _routeEndPoint = routeEndPoint;
            _routeManager = routeManager;
            _logger = logger;

            _receiveProcess = new AsyncProcess(ReceiveProcedure, start: true);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync, AsyncDisposeHelperOptions.Synchronize);
        }

        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return new ValueTask<RouteEndPointAddress>(_routeEndPoint.EndPoint);
        }

        #region Disposal

        /// <inheritdoc />
        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            await _receiveProcess
                .TerminateAsync()
                .HandleExceptionsAsync(_logger)
                .ConfigureAwait(false);

            _routeEndPoint.Dispose();

            await _routeManager
                .RemoveRoutesAsync(_routeEndPoint.EndPoint, removePersistentRoutes: false)
                .HandleExceptionsAsync(_logger)
                .ConfigureAwait(false);
        }
        #endregion

        #region Receive Process

#pragma warning disable VSTHRD200
        private async Task ReceiveProcedure(CancellationToken cancellation)
#pragma warning restore VSTHRD200
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var receiveResult = await _routeEndPoint.ReceiveAsync(cancellation).ConfigureAwait(false);
                    HandleReceiveResultAsync(receiveResult, cancellation).HandleExceptions(_logger);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    _logger?.LogWarning(
                        exc,
                        $"End-point '{localEndPoint}': Exception while processing incoming message.");
                }
            }
        }

        private async Task HandleReceiveResultAsync(
            IRouteEndPointReceiveResult receiveResult,
            CancellationToken cancellation)
        {
            using var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(
                cancellation, receiveResult.Cancellation);

            try
            {
                var routeResult = await HandleAsync(
                    receiveResult.Message, combinedCancellationSource.Token).ConfigureAwait(false);

                if (routeResult.RouteMessage == default)
                {
                    await receiveResult.SendAckAsync().ConfigureAwait(false);
                }
                else
                {
                    await receiveResult.SendResultAsync(routeResult).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) when (receiveResult.Cancellation.IsCancellationRequested)
            {
                await receiveResult.SendCancellationAsync().ConfigureAwait(false);
            }
        }

        private async Task<RouteMessageHandleResult> HandleAsync(Message message, CancellationToken cancellation)
        {
            var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
            var (publish, localDispatch, remoteScope, localScope, route) = DecodeMessage(ref message);

            Debug.Assert(localEndPoint == localScope.EndPointAddress);

            _logger?.LogDebug($"End-point '{localEndPoint}': Processing request message.");
            return await RouteToLocalAsync(
                route,
                new RouteMessage<DispatchDataDictionary>(message),
                publish,
                localDispatch,
                remoteScope,
                localScope,
                cancellation).ConfigureAwait(false);
        }

        #endregion

        private ValueTask<RouteMessageHandleResult> RouteToLocalAsync(
            Route route,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            bool localDispatch,
            RouteEndPointScope remoteScope,
            RouteEndPointScope localScope,
            CancellationToken cancellation)
        {
            return _routeMessageHandler.HandleAsync(
                routeMessage, route, publish, localDispatch, remoteScope, localScope, cancellation);
        }

        public async ValueTask<RouteMessage<IDispatchResult>> RouteAsync(
            Route route,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope remoteScope,
            RouteEndPointScope localScope,
            CancellationToken cancellation)
        {
            if (remoteScope == default)
                throw new ArgumentDefaultException(nameof(remoteScope));

            if (localScope == RouteEndPointScope.NoScope)
            {
                var localEndPointAddress = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
                localScope = new RouteEndPointScope(localEndPointAddress);
            }

            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                var routeResult = await InternalRouteAsync(
                    route,
                    routeMessage,
                    publish,
                    remoteScope,
                    localScope,
                    cancellation).ConfigureAwait(false);

                // TODO: Why do we ignore routeResult.Handled here?

                return routeResult.RouteMessage;
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async ValueTask<IReadOnlyCollection<RouteMessage<IDispatchResult>>> RouteAsync(
            RouteHierarchy routes,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope localScope,
            CancellationToken cancellation)
        {
            if (localScope == RouteEndPointScope.NoScope)
            {
                var localEndPointAddress = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
                localScope = new RouteEndPointScope(localEndPointAddress);
            }

            if (!routes.Any())
            {
                return Array.Empty<RouteMessage<IDispatchResult>>();
            }

            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                return await InternalRouteAsync(
                    routes, routeMessage, publish, localScope, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private async ValueTask<IReadOnlyCollection<RouteMessage<IDispatchResult>>> InternalRouteAsync(
            RouteHierarchy routes,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope localScope,
            CancellationToken cancellation)
        {
            //var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
            List<ValueTask<RouteMessageHandleResult>>? tasks = null;
            var handledEndPoints = new HashSet<RouteEndPointAddress>();
            RouteMessage<IDispatchResult>? lastNonSuccessful = default;

            _logger?.LogTrace($"Routing a message ({(publish ? "publish" : "p2p")}) with routes: {routes}");

            foreach (var route in routes)
            {
                var matches = await MatchRouteAsync(
                    route, publish, handledEndPoints, cancellation).ConfigureAwait(false);

                if (matches.Any())
                {
                    if (!publish)
                    {
                        _logger?.LogTrace($"Found {matches.Count} matches for route '{route}'.");

                        for (var i = matches.Count - 1; i >= 0; i--)
                        {
                            var (endPoint, options) = matches[i];

                            if (endPoint == RouteEndPointAddress.UnknownAddress)
                            {
                                continue;
                            }

                            if ((options & RouteRegistrationOptions.PublishOnly)
                                == RouteRegistrationOptions.PublishOnly)
                            {
                                continue;
                            }

                            var routeResult = await InternalRouteAsync(
                                route,
                                routeMessage,
                                publish: false,
                                remoteScope: new RouteEndPointScope(endPoint),
                                localScope,
                                cancellation).ConfigureAwait(false);

                            if (routeResult.Handled)
                            {
                                return routeResult.RouteMessage.Yield().ToImmutableList();
                            }
                            else
                            {
                                lastNonSuccessful = routeResult.RouteMessage;
                            }
                        }
                    }
                    else
                    {
                        _logger?.LogTrace(
                            $"Found {matches.Count} matches (considering handled end-points) for route '{route}'.");

                        var endPoints = matches.Select(p => p.EndPoint);
                        handledEndPoints.UnionWith(endPoints);

                        tasks ??= new List<ValueTask<RouteMessageHandleResult>>();
                        tasks.AddRange(endPoints.Select(endPoint => InternalRouteAsync(
                            route,
                            routeMessage,
                            publish: true,
                            remoteScope: new RouteEndPointScope(endPoint),
                            localScope,
                            cancellation)));
                    }
                }
            }

            if (!publish && lastNonSuccessful != null)
            {
                return lastNonSuccessful.Value.Yield().ToImmutableList();
            }

            if (tasks is null)
            {
                return ImmutableList<RouteMessage<IDispatchResult>>.Empty;
            }

            var result = await tasks.WhenAll(preserveOrder: false).ConfigureAwait(false);

            _logger?.LogTrace($"Successfully routed a message ({(publish ? "publish" : "p2p")}) with routes: {routes}");

            return result.Where(p => p.Handled).Select(p => p.RouteMessage).ToImmutableList();
        }

        private async Task<List<RouteTarget>> MatchRouteAsync(
            Route route,
            bool publish,
            ISet<RouteEndPointAddress> handledEndPoints,
            CancellationToken cancellation)
        {
            var routeResults = await _routeManager.GetRoutesAsync(route, cancellation);

            if (publish)
            {
                routeResults = routeResults.Where(p => !handledEndPoints.Contains(p.EndPoint));
            }

            var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
            routeResults = routeResults.Where(
                p => localEndPoint == p.EndPoint
                    || !p.RegistrationOptions.IncludesFlag(RouteRegistrationOptions.LocalDispatchOnly));
            return routeResults.ToList();
        }

        private async ValueTask<RouteMessageHandleResult> InternalRouteAsync(
            Route route,
            RouteMessage<DispatchDataDictionary> routeMessage,
            bool publish,
            RouteEndPointScope remoteScope,
            RouteEndPointScope localScope,
            CancellationToken cancellation)
        {
            // The scopes are compatible if they are the same so sourceScope == targetScope or if no target scope 
            // was specified.

            // This does short-circuit the dispatch to the remote end-point. 
            // Any possible replicates do not get any chance to receive the message. 
            // => Requests are kept local.
            if (localScope.CanBeRoutedTo(remoteScope))
            {
                _logger?.LogDebug(
                    $"Message router for end-point '{localScope.EndPointAddress}': Dispatching request message locally.");

                return await RouteToLocalAsync(
                    route,
                    routeMessage,
                    publish,
                    localDispatch: true,
                    localScope, // Reverse local and remote scope order, as we are calling the receiver now.
                    remoteScope,
                    cancellation).ConfigureAwait(false);
            }

            _logger?.LogDebug(
                $"Message router for end-point '{localScope.EndPointAddress}': " +
                $"Dispatching request message to remote end point '{remoteScope.EndPointAddress}'.");

            var message = routeMessage.Message;
            EncodeMessage(ref message, publish, localDispatch: false, remoteScope, localScope, route);

            var result = await _routeEndPoint.SendAsync(
                message,
                remoteScope.EndPointAddress,
                remoteScope.ClusterNodeIdentifier,
                cancellation).ConfigureAwait(false);

            _logger?.LogDebug(
                $"Message router for end-point '{localScope.EndPointAddress}': Processing response message."); // TODO

            return result;
        }

        private static void EncodeMessage(
            ref Message message,
            bool publish,
            bool localDispatch,
            RouteEndPointScope remoteScope,
            RouteEndPointScope localScope,
            in Route route)
        {
            var frameBuilder = new MessageFrameBuilder();

            using (var frameStream = frameBuilder.OpenStream())
            using (var writer = new BinaryWriter(frameStream))
            {
                writer.Write(publish);
                writer.Write(localDispatch);
                RouteEndPointScope.Write(writer, remoteScope);
                RouteEndPointScope.Write(writer, localScope);
                Route.Write(writer, route);
            }

            message = message.PushFrame(frameBuilder.BuildMessageFrame());
        }

        private static (bool publish, bool localDispatch, RouteEndPointScope remoteScope, RouteEndPointScope localScope, Route route) DecodeMessage(ref Message message)
        {
            message = message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new BinaryReader(frameStream);
            var publish = reader.ReadBoolean();
            var localDispatch = reader.ReadBoolean();

            // In EncodeMessage we first write remoteScope, then localScope
            // We now have the reversed order, as we are the receiver now.
            RouteEndPointScope.Read(reader, out var localScope);
            RouteEndPointScope.Read(reader, out var remoteScope);
            Route.Read(reader, out var route);

            return (publish, localDispatch, remoteScope, localScope, route);
        }

        public async Task RegisterRouteAsync(RouteRegistration routeRegistration, CancellationToken cancellation)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
                await _routeManager.AddRouteAsync(localEndPoint, routeRegistration, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task UnregisterRouteAsync(Route route, CancellationToken cancellation)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                var localEndPoint = await GetLocalEndPointAsync(cancellation).ConfigureAwait(false);
                await _routeManager.RemoveRouteAsync(localEndPoint, route, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        public async Task UnregisterRoutesAsync(bool removePersistentRoutes, CancellationToken cancellation)
        {
            try
            {
                using var guard = await _disposeHelper.GuardDisposalAsync(cancellation).ConfigureAwait(false);
                await _routeManager.RemoveRoutesAsync(
                    _routeEndPoint.EndPoint, removePersistentRoutes, cancellation).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }
        }

        private long _nextScopeSeqNum;

        public RouteEndPointScope CreateScope()
        {
            var seqNum = Interlocked.Increment(ref _nextScopeSeqNum);
            return new RouteEndPointScope(_routeEndPoint.EndPoint, _routeEndPoint.ClusterNodeIdentifier, seqNum);
        }

        public bool OwnsScope(RouteEndPointScope scope)
        {
            return scope.EndPointAddress == _routeEndPoint.EndPoint
                && scope.ClusterNodeIdentifier == _routeEndPoint.ClusterNodeIdentifier;
        }
    }
}
