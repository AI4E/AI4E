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
using Nito.AsyncEx;

namespace AI4E.Modularity
{
    // TODO: Route caching and cache coherency
    public sealed class EndPointRouter : IEndPointRouter, IDisposable
    {
        #region Fields

        private readonly AsyncProducerConsumerQueue<IMessage> _rxQueue = new AsyncProducerConsumerQueue<IMessage>();
        private readonly IEndPointManager _endPointManager;
        private readonly IRouteStore _routeStore;
        private readonly AsyncProcess _receiveProcess;

        #endregion

        #region C'tor

        public EndPointRouter(IEndPointManager endPointManager,
                              IRouteStore routeStore,
                              EndPointRoute localEndPoint)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (routeStore == null)
                throw new ArgumentNullException(nameof(routeStore));

            if (localEndPoint == null)
                throw new ArgumentNullException(nameof(localEndPoint));

            _endPointManager = endPointManager;
            _routeStore = routeStore;
            LocalEndPoint = localEndPoint;
            _receiveProcess = new AsyncProcess(ReceiveProcedure);
            _endPointManager.AddEndPoint(LocalEndPoint);
            _receiveProcess.StartExecution();
        }

        #endregion

        public EndPointRoute LocalEndPoint { get; }

        public async Task RegisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _routeStore.AddRouteAsync(LocalEndPoint, messageType, cancellation);
        }

        public async Task UnregisterRouteAsync(string messageType, CancellationToken cancellation)
        {
            await _routeStore.RemoveRouteAsync(LocalEndPoint, messageType, cancellation);
        }

        public Task<IEnumerable<EndPointRoute>> GetRoutesAsync(string messageType, CancellationToken cancellation)
        {
            return _routeStore.GetRoutesAsync(messageType, cancellation);
        }

        public Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, CancellationToken cancellation)
        {
            message.PushFrame();

            return _endPointManager.SendAsync(message, remoteEndPoint, LocalEndPoint, cancellation);
        }

        public Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            response.PushFrame();
            request.PushFrame();

            return _endPointManager.SendAsync(response, request, cancellation);
        }

        public Task<IMessage> ReceiveAsync(CancellationToken cancellation)
        {
            return _rxQueue.DequeueAsync(cancellation);
        }

        private async Task ReceiveProcedure(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await _endPointManager.ReceiveAsync(LocalEndPoint, cancellation);

                    Task.Run(() => HandleMessageAsync(message)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { }
                catch (Exception)
                {
                    // TODO: Log exception
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message)
        {
            message.PopFrame();

            await _rxQueue.EnqueueAsync(message);
        }

        public void Dispose()
        {
            _receiveProcess.TerminateExecution();
            _endPointManager.RemoveEndPoint(LocalEndPoint);
        }
    }
}
