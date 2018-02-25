/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        HandlerRegistration.cs 
 * Types:           AI4E.HandlerRegistration
 *                  AI4E.HandlerRegistrationSource
 *                  AI4E.HandlerRegistrationSource'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.06.2017 
 * Status:          Ready
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

namespace AI4E
{
    public interface IHandlerRegistration : IAsyncCompletion { }

    public interface IHandlerRegistration<THandler> : IHandlerRegistration
    {
        IContextualProvider<THandler> Handler { get; }
    }

    internal sealed class HandlerRegistration<THandler> : IHandlerRegistration<THandler>
    {
        private readonly IHandlerRegistry<THandler> _handlerRegistry;
        private readonly IContextualProvider<THandler> _handlerProvider;
        private readonly TaskCompletionSource<object> _completionSource = new TaskCompletionSource<object>();
        private int _isCompleting = 0;

        public HandlerRegistration(IHandlerRegistry<THandler> handlerRegistry,
                                   IContextualProvider<THandler> handlerProvider)

        {
            if (handlerRegistry == null)
                throw new ArgumentNullException(nameof(handlerRegistry));

            if (handlerProvider == null)
                throw new ArgumentNullException(nameof(handlerProvider));

            _handlerRegistry = handlerRegistry;
            _handlerProvider = handlerProvider;
            _handlerRegistry.Register(_handlerProvider);

            Initialization = Task.CompletedTask;
        }

        public Task Initialization { get; }

        public Task Completion => _completionSource.Task;

        public IContextualProvider<THandler> Handler => _handlerProvider;

        public async void Complete()
        {
            if (Interlocked.Exchange(ref _isCompleting, 1) != 0)
                return;

            try
            {
                _handlerRegistry.Unregister(_handlerProvider);
                _completionSource.SetResult(null);
            }
            catch (TaskCanceledException)
            {
                _completionSource.SetCanceled();
            }
            catch (Exception exc)
            {
                _completionSource.SetException(exc);
            }
        }
    }

    public static class HandlerRegistration
    {
        public static async Task<IHandlerRegistration<THandler>> CreateRegistrationAsync<THandler>(IHandlerRegistry<THandler> handlerRegistry, IContextualProvider<THandler> handlerProvider)
        {
            var registration = new HandlerRegistration<THandler>(handlerRegistry, handlerProvider);

            await registration.Initialization;

            return registration;
        }

        public static async Task<IHandlerRegistration<THandler>> RegisterWithHandleAsync<THandler>(this IHandlerRegistry<THandler> handlerRegistry,
                                                                                                   IContextualProvider<THandler> handlerProvider)
        {
            if (handlerRegistry == null)
                throw new ArgumentNullException(nameof(handlerRegistry));

            if (handlerProvider == null)
                throw new ArgumentNullException(nameof(handlerProvider));

            var registration = new HandlerRegistration<THandler>(handlerRegistry, handlerProvider);

            await registration.Initialization;

            return registration;
        }
    }
}