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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using AI4E.Messaging.Scoping.EndToEndTest.Messages;
using AI4E.Messaging.Scoping.EndToEndTest.Services;

namespace AI4E.Messaging.Scoping.EndToEndTest.Handlers
{
    public sealed class CallbackRegistrationHandler : MessageHandler
    {
        private readonly CallbackRegistry _callbackRegistry;

        public CallbackRegistrationHandler(CallbackRegistry callbackRegistry)
        {
            if (callbackRegistry is null)
                throw new ArgumentNullException(nameof(callbackRegistry));

            _callbackRegistry = callbackRegistry;
        }

        public void Handle(RegisterCallbackCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            _callbackRegistry.RegisterCallback(Context.RemoteScope);
        }

        public void Handle(UnregisterCallbackCommand command)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            _callbackRegistry.UnregisterCallback(Context.RemoteScope);
        }

        public async Task<IDispatchResult> HandleAsync(TriggerCallbackCommand command, CancellationToken cancellation)
        {
            if (command is null)
                throw new ArgumentNullException(nameof(command));

            var callbacks = _callbackRegistry.Callbacks;
            var callbackCommand = new CallbackCommand();
            var dispatchData = DispatchDataDictionary.Create(callbackCommand);

            ValueTask<IDispatchResult> DispatchSingleCallbackAsync(RouteEndPointScope scope)
            {
                return MessageDispatcher.DispatchAsync(
                    dispatchData,
                    publish: false,
                    scope,
                    cancellation);
            }

            var dispatchResults = await callbacks.Select(DispatchSingleCallbackAsync)
                .WhenAll(preserveOrder: false)
                .ConfigureAwait(false);

            return new AggregateDispatchResult(dispatchResults);
        }
    }
}
