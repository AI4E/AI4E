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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Handler
{
    public abstract class InvokerBase<TMessage>
        where TMessage : class
    {
        private readonly IList<IMessageProcessorRegistration> _messageProcessors;
        private readonly IServiceProvider _serviceProvider;

        protected InvokerBase(
            IList<IMessageProcessorRegistration> messageProcessors,
            IServiceProvider serviceProvider)
        {
            if (messageProcessors == null)
                throw new ArgumentNullException(nameof(messageProcessors));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _messageProcessors = messageProcessors;
            _serviceProvider = serviceProvider;
        }

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

        protected virtual bool ExecuteProcessor(IMessageProcessorRegistration messageProcessorRegistration)
        {
            return true;
        }
    }
}
