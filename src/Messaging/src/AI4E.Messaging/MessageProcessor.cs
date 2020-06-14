/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

namespace AI4E.Messaging
{
    /// <summary>
    /// An abstract base class that can be used to implement custom message processors.
    /// </summary>
    public abstract class MessageProcessor : IMessageProcessor
    {

#nullable disable

        /// <summary>
        /// Gets the message processor context.
        /// </summary>
        [MessageProcessorContext]
        public virtual IMessageProcessorContext Context { get; set; }

#nullable restore

        /// <summary>
        /// When overridden in a derviced class, asynchronously processes the specified message.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to process.</typeparam>
        /// <param name="dispatchData">The typed dispatch data dictionary that contains the message.</param>
        /// <param name="next">A function that can be used to invoke the next one in the call chain.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result tha tis the result of the message process operation.
        /// </returns>
        public virtual ValueTask<IDispatchResult> ProcessAsync<TMessage>(DispatchDataDictionary<TMessage> dispatchData,
                                                                         Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
                                                                         CancellationToken cancellation)
            where TMessage : class
        {
            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            if (next == null)
                throw new ArgumentNullException(nameof(next));

            return next(dispatchData);
        }
    }
}
