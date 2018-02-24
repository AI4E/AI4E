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

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Internal
{
    internal sealed class MessageHandlerProvider<TMessage> : IContextualProvider<IMessageHandler<TMessage>>
    {
        private readonly Type _type;
        private readonly MessageHandlerActionDescriptor _actionDescriptor;
        private readonly ImmutableArray<IContextualProvider<IMessageProcessor>> _processors;

        public MessageHandlerProvider(Type type, MessageHandlerActionDescriptor actionDescriptor, ImmutableArray<IContextualProvider<IMessageProcessor>> processors)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _type = type;
            _actionDescriptor = actionDescriptor;
            _processors = processors;
        }

        public IMessageHandler<TMessage> ProvideInstance(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            // Create a new instance of the handler type.
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, _type);

            Debug.Assert(handler != null);

            return new MessageHandlerInvoker<TMessage>(handler, _actionDescriptor, _processors, serviceProvider);
        }
    }
}
