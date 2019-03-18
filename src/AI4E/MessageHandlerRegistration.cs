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
    public sealed class MessageHandlerRegistration : IMessageHandlerRegistration
    {
        private readonly Func<IServiceProvider, IMessageHandler> _factory;

        public MessageHandlerRegistration(Type messageType, Func<IServiceProvider, IMessageHandler> factory)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            MessageType = messageType;
            _factory = factory;
            Configuration = default;
        }

        public MessageHandlerRegistration(Type messageType, MessageHandlerConfiguration configuration, Func<IServiceProvider, IMessageHandler> factory)
          : this(messageType, factory)
        {
            Configuration = configuration;
        }

        public IMessageHandler CreateMessageHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var result = _factory(serviceProvider);

            if (result == null)
                throw new InvalidOperationException("The handler provided must not be null.");

            if (result.MessageType != MessageType)
                throw new InvalidOperationException($"The handler provided must handle messages of type {MessageType}.");

            return result;
        }

        public Type MessageType { get; }

        public MessageHandlerConfiguration Configuration { get; }
    }

    public sealed class MessageHandlerRegistration<TMessage> : IMessageHandlerRegistration<TMessage>
        where TMessage : class
    {
        private static readonly Type _messageType = typeof(TMessage);
        private readonly Func<IServiceProvider, IMessageHandler<TMessage>> _factory;

        public MessageHandlerRegistration(Func<IServiceProvider, IMessageHandler<TMessage>> factory)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
            Configuration = default;
        }

        public MessageHandlerRegistration(MessageHandlerConfiguration configuration, Func<IServiceProvider, IMessageHandler<TMessage>> factory)
           : this(factory)
        {

            Configuration = configuration;
        }

        public IMessageHandler<TMessage> CreateMessageHandler(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var result = _factory(serviceProvider);

            if (result == null)
                throw new InvalidOperationException("The handler provided must not be null.");

            return result;
        }

        IMessageHandler IMessageHandlerRegistration.CreateMessageHandler(IServiceProvider serviceProvider)
        {
            return CreateMessageHandler(serviceProvider);
        }

        Type IMessageHandlerRegistration.MessageType => _messageType;

        public MessageHandlerConfiguration Configuration { get; }
    }
}
