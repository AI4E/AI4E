/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        HandlerRegistration.cs 
 * Types:           AI4E.HandlerRegistration
 *                  AI4E.HandlerRegistrationSource
 *                  AI4E.HandlerRegistrationSource'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
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

namespace AI4E
{
    /// <summary>
    /// Represents a cancellable handler registration.
    /// </summary>
    public interface IHandlerRegistration
    {
        /// <summary>
        /// Gets a task that represents the asynchronous cancellation of the registration.
        /// </summary>
        Task Cancellation { get; }

        /// <summary>
        /// Cancels the registration.
        /// </summary>
        void Cancel();
    }

    /// <summary>
    /// Represents a cancellable handler registration of the specified type of handler.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IHandlerRegistration<THandler> : IHandlerRegistration
    {
        /// <summary>
        /// Gets a contextual provider that provides instances of the registered handler.
        /// </summary>
        IContextualProvider<THandler> Handler { get; }
    }

    internal sealed class HandlerRegistration<THandler> : IHandlerRegistration<THandler>
    {
        private readonly IHandlerRegistry<THandler> _handlerRegistry;
        private readonly IContextualProvider<THandler> _handlerProvider;
        private readonly TaskCompletionSource<object> _cancellationSource = new TaskCompletionSource<object>();
        private int _isCancelling = 0;

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
        }

        public Task Cancellation => _cancellationSource.Task;

        public IContextualProvider<THandler> Handler => _handlerProvider;

        public void Cancel()
        {
            if (Interlocked.Exchange(ref _isCancelling, 1) != 0)
                return;

            try
            {
                _handlerRegistry.Unregister(_handlerProvider);
                _cancellationSource.SetResult(null);
            }
            catch (TaskCanceledException)
            {
                _cancellationSource.SetCanceled();
            }
            catch (Exception exc)
            {
                _cancellationSource.SetException(exc);
            }
        }
    }

    /// <summary>
    /// Provides method for creating handler registrations.
    /// </summary>
    public static class HandlerRegistration
    {
        /// <summary>
        /// Asynchronously registers the specified handler in the specified handler registry and returns a handler registration.
        /// </summary>
        /// <typeparam name="THandler">The type of handler.</typeparam>
        /// <param name="handlerRegistry">The handler registry that the handler shall be registered to.</param>
        /// <param name="handlerProvider">A contextual provider that provides instances of the to be registered handler.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static IHandlerRegistration<THandler> CreateRegistration<THandler>(
            this IHandlerRegistry<THandler> handlerRegistry, 
            IContextualProvider<THandler> handlerProvider)
        {
            return new HandlerRegistration<THandler>(handlerRegistry, handlerProvider);
        }
    }
}
