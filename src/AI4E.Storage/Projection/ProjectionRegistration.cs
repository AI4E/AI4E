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
using AI4E.Utils;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Provides method for creating handler registrations.
    /// </summary>
    public static class ProjectionRegistration
    {
        /// <summary>
        /// Asynchronously registers the specified handler in the specified handler registry and returns a handler registration.
        /// </summary>
        /// <typeparam name="THandler">The type of handler.</typeparam>
        /// <param name="handlerRegistry">The handler registry that the handler shall be registered to.</param>
        /// <param name="handlerProvider">A contextual provider that provides instances of the to be registered handler.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        public static IProjectionRegistration<THandler> CreateRegistration<THandler>(
            this IProjectionRegistry<THandler> handlerRegistry,
            IContextualProvider<THandler> handlerProvider)
        {
            return new TypedProjectionRegistration<THandler>(handlerRegistry, handlerProvider);
        }

        private sealed class TypedProjectionRegistration<TProjection> : IProjectionRegistration<TProjection>
        {
            private readonly IProjectionRegistry<TProjection> _handlerRegistry;
            private readonly TaskCompletionSource<object> _cancellationSource = new TaskCompletionSource<object>();
            private int _isCancelling = 0;

            public TypedProjectionRegistration(IProjectionRegistry<TProjection> projectionRegistry,
                                               IContextualProvider<TProjection> projectionProvider)

            {
                if (projectionRegistry == null)
                    throw new ArgumentNullException(nameof(projectionRegistry));

                if (projectionProvider == null)
                    throw new ArgumentNullException(nameof(projectionProvider));

                _handlerRegistry = projectionRegistry;
                Projection = projectionProvider;
                _handlerRegistry.Register(Projection);
            }

            public Task Initialization => Task.CompletedTask;

            public IContextualProvider<TProjection> Projection { get; }

            public void Dispose()
            {
                if (Interlocked.Exchange(ref _isCancelling, 1) != 0)
                    return;

                try
                {
                    _handlerRegistry.Unregister(Projection);
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

            public ValueTask DisposeAsync()
            {
                Dispose();
                return Disposal.AsValueTask();
            }

            public Task Disposal => _cancellationSource.Task;
        }
    }
}
