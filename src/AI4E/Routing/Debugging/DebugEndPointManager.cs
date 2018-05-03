/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        DebugEndPointManager.cs 
 * Types:           AI4E.Routing.Debugging.DebugEndPointManager
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   01.05.2018 
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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Proxying;
using AI4E.Remoting;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.Debugging
{
    public sealed class DebugEndPointManager : IEndPointManager, IAsyncDisposable
    {
        private readonly ProxyHost _proxyHost;
        private readonly AsyncInitializationHelper<IProxy<EndPointManagerSkeleton>> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public DebugEndPointManager(ProxyHost proxyHost)
        {
            if (proxyHost == null)
                throw new ArgumentNullException(nameof(proxyHost));

            _proxyHost = proxyHost;
            _initializationHelper = new AsyncInitializationHelper<IProxy<EndPointManagerSkeleton>>(
                async cancellation => await _proxyHost.ActivateAsync<EndPointManagerSkeleton>(ActivationMode.Create, cancellation));
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private Task<IProxy<EndPointManagerSkeleton>> GetProxyAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation);
        }

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            var (success, proxy) = await _initializationHelper.CancelAsync().HandleExceptionsAsync();

            if (success)
            {
                Assert(proxy != null);

                await proxy.DisposeAsync();
            }
        }

        private void CheckDisposal()
        {
            if (_disposeHelper.IsDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        #endregion

        public async Task AddEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                await proxy.ExecuteAsync(p => p.AddEndPointAsync(route, cancelledOrDisposed));
            }
        }

        public async Task RemoveEndPointAsync(EndPointRoute route, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                await proxy.ExecuteAsync(p => p.RemoveEndPointAsync(route, cancelledOrDisposed));
            }
        }

        public async Task<IMessage> ReceiveAsync(EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                return await proxy.ExecuteAsync(p => p.ReceiveAsync(localEndPoint, cancelledOrDisposed));
            }
        }

        public async Task SendAsync(IMessage message, EndPointRoute remoteEndPoint, EndPointRoute localEndPoint, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                await proxy.ExecuteAsync(p => p.SendAsync(message, remoteEndPoint, localEndPoint, cancelledOrDisposed));
            }
        }

        public async Task SendAsync(IMessage response, IMessage request, CancellationToken cancellation)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                CheckDisposal();

                var cancelledOrDisposed = _disposeHelper.CancelledOrDisposed(cancellation);
                var proxy = await GetProxyAsync(cancelledOrDisposed);

                await proxy.ExecuteAsync(p => p.SendAsync(response, request, cancelledOrDisposed));
            }
        }
    }
}
