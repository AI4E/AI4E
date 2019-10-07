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

namespace AI4E.Messaging.MessageHandlers
{
    /// <summary>
    /// Represents the context of a message dispatch operation.
    /// </summary>
    public sealed class MessageDispatchContext : IMessageDispatchContext
    {
        /// <summary>
        /// Creates a new instance of the <see cref="MessageDispatchContext"/> type.
        /// </summary>
        /// <param name="dispatchServices">The dispatch operations service provider.</param>
        /// <param name="dispatchData">The dispatch data of the current dispatch operation.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="isLocalDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        public MessageDispatchContext(
            IServiceProvider dispatchServices,
            DispatchDataDictionary dispatchData,
            bool publish,
            bool isLocalDispatch)
        {
            if (dispatchServices == null)
                throw new ArgumentNullException(nameof(dispatchServices));

            if (dispatchData == null)
                throw new ArgumentNullException(nameof(dispatchData));

            DispatchServices = dispatchServices;
            DispatchData = dispatchData;
            IsPublish = publish;
            IsLocalDispatch = isLocalDispatch;
        }

        /// <inheritdoc/>
        public IServiceProvider DispatchServices { get; }

        /// <inheritdoc/>
        public DispatchDataDictionary DispatchData { get; }

        /// <inheritdoc/>
        public bool IsPublish { get; }

        /// <inheritdoc/>
        public bool IsLocalDispatch { get; }
    }
}
