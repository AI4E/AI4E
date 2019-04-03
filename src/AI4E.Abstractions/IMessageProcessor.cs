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
    /// Represents a message processor.
    /// </summary>
    public interface IMessageProcessor
    {
        /// <summary>
        /// Processes a message before and after it is handled.
        /// </summary>
        /// <typeparam name="TMessage">The type of message.</typeparam>
        /// <param name="dispatchData">The dispatch data of the current dispatch operation.</param>
        /// <param name="next">A function that invokes the next processor or the message handler respectively.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the dispatch result.
        /// </returns>
        ValueTask<IDispatchResult> ProcessAsync<TMessage>(DispatchDataDictionary<TMessage> dispatchData,
                                                          Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
                                                          CancellationToken cancellation)
            where TMessage : class;
    }

    /// <summary>
    /// Represents the context of a message processor.
    /// </summary>
    public interface IMessageProcessorContext
    {
        /// <summary>
        /// Gets the message handler configuration.
        /// </summary>
        MessageHandlerConfiguration MessageHandlerConfiguration { get; }

        /// <summary>
        /// Gets a descriptor that identifies the message handler.
        /// </summary>
        MessageHandlerActionDescriptor MessageHandlerAction { get; }

        /// <summary>
        /// Gets the message handler instance.
        /// </summary>
        object MessageHandler { get; }

        /// <summary>
        /// Gets a boolean value specifying whether the message is published to all handlers.
        /// </summary>
        bool IsPublish { get; }

        /// <summary>
        /// Gets a boolean value specifying whether the message is dispatched locally.
        /// </summary>
        bool IsLocalDispatch { get; }
    }

    /// <summary>
    /// An attribute that identifies a message processor's context property.
    /// </summary>
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
    public sealed class MessageProcessorContextAttribute : Attribute { }
}
