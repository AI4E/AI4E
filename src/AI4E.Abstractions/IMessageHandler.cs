/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IMessageHandler.cs
 * Types:           AI4E.IMessageHandler'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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

using System.Threading;
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Represents a message handler that can handle messages of the specified type.
    /// </summary>
    /// <typeparam name="TMessage">The type of message.</typeparam>
    public interface IMessageHandler<TMessage>
    {
        /// <summary>
        /// Asynchronously handles the specified message.
        /// </summary>
        /// <param name="message">The message to be handled.</param>
        /// <param name="context">The message context.</param>
        /// <returns>A task representing the asynchronous operation. The task contains the dispatch result on evaluation.</returns>
        Task<IDispatchResult> HandleAsync(TMessage message, DispatchValueDictionary context);
    }
}
