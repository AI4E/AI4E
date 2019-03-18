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

namespace AI4E
{
    /// <summary>
    /// Represents a message dispatcher that dispatches messages to message handlers.
    /// </summary>
    public interface IMessageDispatcher
    {
        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="dispatchData">The dispatchd data dictionary that contains the message and supporting values.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="messageType"/> or <paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="message"/> is not of type <paramref name="messageType"/> or a derived type.</exception>
        ValueTask<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation = default);
    }
}
