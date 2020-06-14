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
using System.Collections.Generic;

namespace AI4E.Messaging
{
    /// <summary>
    /// Implements the null-object design pattern for the <see cref="IMessageHandlerProvider"/> interface.
    /// </summary>
    public sealed class NoMessageHandlerProvider : IMessageHandlerProvider
    {
        /// <summary>
        /// Gets the singleton instance of the <see cref="NoMessageHandlerProvider"/> type.
        /// </summary>
        public static NoMessageHandlerProvider Instance { get; } = new NoMessageHandlerProvider();

        private NoMessageHandlerProvider() { }

        /// <inheritdoc />
        public IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations(Type messageType)
        {
            return Array.Empty<IMessageHandlerRegistration>();
        }

        /// <inheritdoc />
        public IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations()
        {
            return Array.Empty<IMessageHandlerRegistration>();
        }
    }
}
