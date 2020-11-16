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

namespace AI4E.Messaging
{
    /// <summary>
    /// Contains extensions for the <see cref="IMessagingEngine"/> type.
    /// </summary>
    public static class MessagingEngineExtension
    {
        /// <summary>
        /// Creates a message dispatcher using the specified messaging engine.
        /// </summary>
        /// <param name="messagingEngine">
        /// The <see cref="IMessagingEngine"/> used to create the message dispatcher.
        /// </param>
        /// <returns>
        /// An instance of <see cref="IMessageDispatcher"/> that can be used to dispatch messages.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="messagingEngine"/> is <c>null</c>.
        /// </exception>
        /// <remarks>
        /// The message engine`s service provider is used for the message dispatcher.
        /// </remarks>
        public static IMessageDispatcher CreateDispatcher(this IMessagingEngine messagingEngine)
        {
            if (messagingEngine is null)
                throw new ArgumentNullException(nameof(messagingEngine));

            return messagingEngine.CreateDispatcher(messagingEngine.ServiceProvider);
        }
    }
}
