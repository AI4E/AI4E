/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Utils.Async;

namespace AI4E.Messaging
{
    public sealed class MessageDispatcher : IMessageDispatcher
    {
        private readonly AsyncInitializationHelper<RouteEndPointScope> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        internal MessageDispatcher(
            MessagingEngine owner,
            IServiceProvider serviceProvider)
        {
            Debug.Assert(owner != null);

            Engine = owner;
            ServiceProvider = serviceProvider;
            _initializationHelper = new AsyncInitializationHelper<RouteEndPointScope>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        private async Task<RouteEndPointScope> InitializeInternalAsync(CancellationToken cancellation)
        {
            var messageRouter = await Engine.GetMessageRouterAsync(cancellation).ConfigureAwait(false);

            RouteEndPointScope scope;

            do
            {
                cancellation.ThrowIfCancellationRequested();
                scope = messageRouter.CreateScope();
            }
            while (!Engine.RegisterScopedDispatcher(scope, this));

            return scope;
        }

        public MessagingEngine Engine { get; }

        IMessagingEngine IMessageDispatcher.Engine => Engine;

        public ValueTask<RouteEndPointScope> GetScopeAsync(CancellationToken cancellation = default)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation).AsValueTask();
        }

        public IServiceProvider ServiceProvider { get; }

        // Currently scoped dispatchers cannot alter the message handler provider table.
        public IMessageHandlerProvider MessageHandlerProvider => Engine.MessageHandlerProvider;

        public async ValueTask<IDispatchResult> DispatchAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            RouteEndPointScope remoteScope,
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            var localScope = await GetScopeAsync(cancellation).ConfigureAwait(false);

            return await Engine.DispatchAsync(
                dispatchData,
                publish,
                remoteScope,
                localScope,
                cancellation).ConfigureAwait(false);
        }

        public async ValueTask<IDispatchResult> DispatchLocalAsync(
            DispatchDataDictionary dispatchData,
            bool publish,
            CancellationToken cancellation)
        {
            var localScope = await GetScopeAsync(cancellation).ConfigureAwait(false);

            return await DispatchAsync(dispatchData, publish, localScope, cancellation).ConfigureAwait(false);
        }

        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation)
        {
            return Engine.GetLocalEndPointAsync(cancellation);
        }

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public ValueTask DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            var (success, scope) = await _initializationHelper.CancelAsync().ConfigureAwait(false);

            if (success)
            {
                Engine.UnregisterScopedDispatcher(scope, this);
            }
        }
    }
}
