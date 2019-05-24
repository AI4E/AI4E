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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Handler
{
    /// <summary>
    /// A base class for invokers that need message processor chaining support.
    /// </summary>
    /// <typeparam name="TMessage">The type of message that is dispatched.</typeparam>
    public abstract class InvokerBase<TMessage>
        where TMessage : class
    {
        private readonly List<IMessageProcessorRegistration> _messageProcessors;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="InvokerBase{TMessage}"/> type.
        /// </summary>
        /// <param name="messageProcessors">A collection of <see cref="IMessageProcessor"/>s to call.</param>
        /// <param name="serviceProvider">>A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageProcessors"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        protected InvokerBase(
            IEnumerable<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (messageProcessors == null)
                throw new ArgumentNullException(nameof(messageProcessors));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _messageProcessors = AsList(messageProcessors.TopologicalSort(p => messageProcessors.Where(p.Dependency.IsDependency), throwOnCycle: true));
            _serviceProvider = serviceProvider;
        }

        private static List<IMessageProcessorRegistration> AsList(IEnumerable<IMessageProcessorRegistration> messageProcessors)
        {
            if (messageProcessors is List<IMessageProcessorRegistration> result)
            {
                return result;
            }

            return messageProcessors.ToList();
        }

        /// <summary>
        /// Executes the message processor chain.
        /// </summary>
        /// <param name="handler">The message handler.</param>
        /// <param name="memberDescriptor">The message handler action descriptor.</param>
        /// <param name="dispatchData">The dispatch data dictionary that contains the message.</param>
        /// <param name="publish">A boolean value indicating whether the message sent via publish-subscribe.</param>
        /// <param name="localDispatch">A boolean value indicating whether the dispatch operation is locally.</param>
        /// <param name="invokeCore">An asynchronous function that invokes the message handler.</param>
        /// <param name="cancellation">A cancellation token.</param>
        /// <returns>
        /// A <see cref="ValueTask{TResult}"/> representing the asynchronous operation.
        /// </returns>
        protected ValueTask<IDispatchResult> InvokeChainAsync(
            object handler,
            MessageHandlerActionDescriptor memberDescriptor,
            DispatchDataDictionary<TMessage> dispatchData,
            bool publish,
            bool localDispatch,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> invokeCore,
            CancellationToken cancellation)
        {
            var next = invokeCore;

            for (var i = _messageProcessors.Count - 1; i >= 0; i--)
            {
                if (!ExecuteProcessor(_messageProcessors[i]))
                {
                    continue;
                }

                var processor = _messageProcessors[i].CreateMessageProcessor(_serviceProvider);
                Debug.Assert(processor != null);
                var nextCopy = next; // This is needed because of the way, the variable values are captured in the lambda expression.

                ValueTask<IDispatchResult> InvokeProcessor(DispatchDataDictionary<TMessage> nextDispatchData)
                {
                    var contextDescriptor = MessageProcessorContextDescriptor.GetDescriptor(processor.GetType());

                    if (contextDescriptor.CanSetContext)
                    {
                        IMessageProcessorContext messageProcessorContext = new MessageProcessorContext(handler, memberDescriptor, publish, localDispatch);

                        contextDescriptor.SetContext(processor, messageProcessorContext);
                    }

                    return processor.ProcessAsync(nextDispatchData, nextCopy, cancellation);
                }

                next = InvokeProcessor;
            }

            return next(dispatchData);
        }

        /// <summary>
        /// Returns a boolean value specifying whether a message processor shall be executed.
        /// </summary>
        /// <param name="messageProcessorRegistration">Describes the message processor to execute.</param>
        /// <returns>True if the message processor described by <paramref name="messageProcessorRegistration"/> shall be executed, false otherwise.</returns>
        protected virtual bool ExecuteProcessor(IMessageProcessorRegistration messageProcessorRegistration)
        {
            return true;
        }
    }
}
