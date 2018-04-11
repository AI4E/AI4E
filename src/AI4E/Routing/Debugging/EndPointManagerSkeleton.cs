/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EndPointManagerSkeleton.cs 
 * Types:           AI4E.Routing.Debugging.EndPointManagerSkeleton
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
using AI4E.Modularity;
using Nito.AsyncEx;

namespace AI4E.Routing.Debugging
{
    public sealed class EndPointManagerSkeleton : IAsyncDisposable
    {
        private readonly IEndPointManager _endPointManager;
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly HashSet<EndPointRoute> _addedEndPoints = new HashSet<EndPointRoute>();
        private readonly AsyncLock _lock = new AsyncLock();
        private readonly AsyncDisposeHelper _disposeHelper;

        public EndPointManagerSkeleton(IEndPointManager endPointManager, IMessageDispatcher messageDispatcher)
        {
            if (endPointManager == null)
                throw new ArgumentNullException(nameof(endPointManager));

            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _endPointManager = endPointManager;
            _messageDispatcher = messageDispatcher;

            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region Disposal

        private async Task DisposeInternalAsync()
        {
            Console.WriteLine("Disposing EndPointManagerSkeleton");

            using (await _lock.LockAsync())
            {
                foreach (var endPoint in _addedEndPoints)
                {
                    await _endPointManager.RemoveEndPointAsync(endPoint, cancellation: default);

                    await _messageDispatcher.DispatchAsync(new EndPointDisconnected(endPoint), publish: true);
                }
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

        public Task Disposal => _disposeHelper.Disposal;

        #endregion

        public async Task AddEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (await _lock.LockAsync())
                {
                    if (_addedEndPoints.Contains(route))
                        return;

                    await _endPointManager.AddEndPointAsync(route, cancellation);
                    _addedEndPoints.Add(route);

                    await _messageDispatcher.DispatchAsync(new EndPointConnected(route), publish: true);
                }
            }
        }

        public async Task RemoveEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                using (await _lock.LockAsync())
                {
                    if (!_addedEndPoints.Contains(route))
                        return;

                    await _endPointManager.RemoveEndPointAsync(route, cancellation);
                    _addedEndPoints.Remove(route);

                    await _messageDispatcher.DispatchAsync(new EndPointDisconnected(route), publish: true);
                }
            }
        }

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                return await _endPointManager.ReceiveAsync(localEndPoint, cancellation);
            }
        }
        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _endPointManager.SendAsync(message, remoteEndPoint, localEndPoint, cancellation);
            }
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync())
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _endPointManager.SendAsync(response, request, cancellation);
            }
        }
    }
}
