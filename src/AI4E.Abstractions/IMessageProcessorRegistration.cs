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

namespace AI4E
{
    /// <summary>
    /// Represents the registration of a message processor.
    /// </summary>
    public interface IMessageProcessorRegistration
    {
        /// <summary>
        /// Creates an instance of the registered message processor within the scope of the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> that is used to obtain processor specific services.</param>
        /// <returns>The created <see cref="IMessageProcessor"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        IMessageProcessor CreateMessageProcessor(IServiceProvider serviceProvider);

        /// <summary>
        /// Gets the message processor type.
        /// </summary>
        Type MessageProcessorType { get; }

        /// <summary>
        /// Gets the message processor dependency descriptor.
        /// </summary>
        MessageProcessorDependency Dependency { get; }
    }
}
