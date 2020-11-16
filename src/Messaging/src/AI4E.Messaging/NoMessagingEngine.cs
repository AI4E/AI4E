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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging.Routing;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Messaging
{
    /// <summary>
    /// Implements the null-object design pattern for the <see cref="IMessagingEngine"/> interface.
    /// </summary>
    public sealed class NoMessagingEngine : IMessagingEngine
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="NoMessagingEngine"/> type.
        /// </summary>
        public static NoMessagingEngine Instance { get; } = new NoMessagingEngine();

        private NoMessagingEngine() { }

        /// <inheritdoc />
        public IMessageDispatcher CreateDispatcher(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            return new NoMessageDispatcher(this, serviceProvider);
        }

        IMessageHandlerProvider IMessagingEngine.MessageHandlerProvider => MessageHandlerProvider;

        /// <summary>
        /// Gets the message handler provider, 
        /// which is always the singleton instance of <see cref="NoMessageHandlerProvider"/>.
        /// </summary>
#pragma warning disable CA1822
        public NoMessageHandlerProvider MessageHandlerProvider => NoMessageHandlerProvider.Instance;
#pragma warning restore CA1822

        /// <inheritdoc />
        public ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default)
        {
            return new ValueTask<RouteEndPointAddress>(result: default);
        }

        /// <inheritdoc />
        public Task Initialization => Task.CompletedTask;

        /// <inheritdoc />
        public void Dispose() { }

        /// <inheritdoc />
        public ValueTask DisposeAsync()
        {
            return default;
        }

        public IServiceProvider ServiceProvider { get; } = new ServiceCollection().BuildServiceProvider();
    }
}
