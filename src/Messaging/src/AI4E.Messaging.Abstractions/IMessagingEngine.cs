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

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents messaging engine that message dispatchers can be created from.
    /// </summary>
    public interface IMessagingEngine : IDisposable, IAsyncDisposable
    {
        /// <summary>
        /// Creates a message dispatcher using the engine.
        /// </summary>
        /// <param name="serviceProvider">
        /// The <see cref="IServiceProvider"/> of the  message dispatcher used to resolve services.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IMessageDispatcher"/> that can be used to dispatch messages.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        IMessageDispatcher CreateDispatcher(IServiceProvider serviceProvider);

        /// <summary>
        /// Gets the <see cref="IMessageHandlerProvider"/> that is used to load (local) message handlers.
        /// </summary>
        IMessageHandlerProvider MessageHandlerProvider { get; }

        /// <summary>
        /// Gets the <see cref="IServiceProvider"/> of the messaging engine that is used to resolve services.
        /// </summary>
        IServiceProvider ServiceProvider { get; }

        /// <summary>
        /// Asynchronously retrieves the local end-point of the message dispatcher.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation,
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{RouteEndPointAddress}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the local end-point of the message dispatcher.
        /// </returns>
        ValueTask<RouteEndPointAddress> GetLocalEndPointAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Gets a task representing the asynchronous initialization of the instance.
        /// </summary>
        Task Initialization { get; }
    }
}
