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

using System.Threading;
using System.Threading.Tasks;
using AI4E.Validation;

namespace AI4E
{
    /// <summary>
    /// Contains validation specific extensions for the <see cref="IMessageDispatcher"/> type.
    /// </summary>
    public static class ValidationMessageDispatcherExtension
    {
        /// <summary>
        /// Asynchronously performs a validation dispatch for the specified set of data.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to validate.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="dispatchData">The dispatch data that contains the message to validate.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> ValidateAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            DispatchDataDictionary<TMessage> dispatchData,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            var validationDispatchData = new DispatchDataDictionary<Validate<TMessage>>(
                message: new Validate<TMessage>(dispatchData.Message),
                data: dispatchData);

            return messageDispatcher.DispatchAsync(validationDispatchData, publish: false, cancellation);
        }

        /// <summary>
        /// Asynchronously performs a validation dispatch for the specified set of data.
        /// </summary>
        /// <typeparam name="TMessage">The type of message to validate.</typeparam>
        /// <param name="messageDispatcher">The message dispatcher.</param>
        /// <param name="message">The message to validate.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        public static ValueTask<IDispatchResult> ValidateAsync<TMessage>(
            this IMessageDispatcher messageDispatcher,
            TMessage message,
            CancellationToken cancellation = default)
            where TMessage : class
        {
            var validationDispatchData = new DispatchDataDictionary<Validate<TMessage>>(
                new Validate<TMessage>(message));

            return messageDispatcher.DispatchAsync(validationDispatchData, publish: false, cancellation);
        }
    }
}
