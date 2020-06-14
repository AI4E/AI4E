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

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents the registration of a message handler.
    /// </summary>
    public sealed class MessageHandlerRegistration : IMessageHandlerRegistration
    {
        private readonly Func<IServiceProvider, IMessageHandler> _factory;
        private readonly MessageHandlerActionDescriptor? _descriptor;

        /// <summary>
        /// Creates a new instance of the <see cref="MessageHandlerRegistration"/> type.
        /// </summary>
        /// <param name="messageType">The type of message the handler can handle.</param>
        /// <param name="factory">A factory function that is used to obtain the message handler.</param>
        /// <param name="descriptor">A <see cref="MessageHandlerActionDescriptor"/> that described the message handler, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageType"/> or <paramref name="factory"/> is <c>null</c>.
        /// </exception>
        public MessageHandlerRegistration(
            Type messageType,
            Func<IServiceProvider, IMessageHandler> factory,
            MessageHandlerActionDescriptor? descriptor = null)
        {
            if (messageType == null)
                throw new ArgumentNullException(nameof(messageType));

            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            MessageType = messageType;
            _factory = factory;
            Configuration = default;
            _descriptor = descriptor;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageHandlerRegistration"/> type.
        /// </summary>
        /// <param name="messageType">The type of message the handler can handle.</param>
        /// <param name="configuration">The message handler configuration.</param>
        /// <param name="factory">A factory function that is used to obtain the message handler.</param>
        /// <param name="descriptor">A <see cref="MessageHandlerActionDescriptor"/> that described the message handler, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageType"/> or <paramref name="factory"/> is <c>null</c>.
        /// </exception>
        public MessageHandlerRegistration(
            Type messageType,
            MessageHandlerConfiguration configuration,
            Func<IServiceProvider, IMessageHandler> factory,
            MessageHandlerActionDescriptor? descriptor = null)
          : this(messageType, factory, descriptor)
        {
            Configuration = configuration;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public Type MessageType { get; }

        /// <inheritdoc />
        public MessageHandlerConfiguration Configuration { get; }

        /// <inheritdoc />
        public bool TryGetDescriptor(out MessageHandlerActionDescriptor descriptor)
        {
            descriptor = _descriptor.GetValueOrDefault();
            return _descriptor.HasValue;
        }
    }

    /// <summary>
    /// Represents the registration of a message handler.
    /// </summary>
    /// <typeparam name="TMessage">The type of message the handler can handle.</typeparam>
    public sealed class MessageHandlerRegistration<TMessage> : IMessageHandlerRegistration<TMessage>
        where TMessage : class
    {
        private static readonly Type _messageType = typeof(TMessage);
        private readonly Func<IServiceProvider, IMessageHandler<TMessage>> _factory;
        private readonly MessageHandlerActionDescriptor? _descriptor;

        /// <summary>
        /// Creates a new instance of the <see cref="MessageHandlerRegistration{TMessage}"/> type.
        /// </summary>
        /// <param name="factory">A factory function that is used to obtain the message handler.</param>
        /// <param name="descriptor">A <see cref="MessageHandlerActionDescriptor"/> that described the message handler, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is <c>null</c>.</exception>
        public MessageHandlerRegistration(
            Func<IServiceProvider, IMessageHandler<TMessage>> factory,
            MessageHandlerActionDescriptor? descriptor = null)
        {
            if (factory == null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
            Configuration = default;
            _descriptor = descriptor;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageHandlerRegistration{TMessage}"/> type.
        /// </summary>
        /// <param name="configuration">The message handler configuration.</param>
        /// <param name="factory">A factory function that is used to obtain the message handler.</param>
        /// <param name="descriptor">A <see cref="MessageHandlerActionDescriptor"/> that described the message handler, or <c>null</c>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is <c>null</c>.</exception>
        public MessageHandlerRegistration(
            MessageHandlerConfiguration configuration,
            Func<IServiceProvider, IMessageHandler<TMessage>> factory,
            MessageHandlerActionDescriptor? descriptor = null)
           : this(factory, descriptor)
        {

            Configuration = configuration;
        }

        /// <inheritdoc />
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

        /// <inheritdoc />
        public MessageHandlerConfiguration Configuration { get; }

        /// <inheritdoc />
        public bool TryGetDescriptor(out MessageHandlerActionDescriptor descriptor)
        {
            descriptor = _descriptor.GetValueOrDefault();
            return _descriptor.HasValue;
        }
    }
}
