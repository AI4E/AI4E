/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IMessageDispatcher.cs
 * Types:           AI4E.IMessageDispatcher
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   09.09.2018 
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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Represents a message dispatcher that dispatches messages to message handlers.
    /// </summary>
    public interface IMessageDispatcher
    {
        /// <summary>
        /// Asynchronously dispatches a message of the specified message type.
        /// </summary>
        /// <param name="dispatchData">The dispatchd data dictionary that contains the message and supporting values.</param>
        /// <param name="publish">A boolean value specifying whether the message shall be published to all handlers.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// The tasks result contains an <see cref="IDispatchResult"/> indicating message handling state.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="messageType"/> or <paramref name="message"/> is null.</exception>
        /// <exception cref="ArgumentException">Thrown if <paramref name="message"/> is not of type <paramref name="messageType"/> or a derived type.</exception>
        Task<IDispatchResult> DispatchAsync(DispatchDataDictionary dispatchData, bool publish, CancellationToken cancellation = default); // TODO: Return ValueTask<IDispatchResult>

        /// <summary>
        /// Registers a message handler.
        /// </summary>
        /// <param name="messageType">The type of message.</param>
        /// <param name="messageHandlerProvider">The message handler provider to register.</param>
        /// <returns>
        /// A <see cref="IHandlerRegistration"/> that represents the handlers registration.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="messageType"/> or <paramref name="messageHandlerProvider"/> is null.</exception>
        IHandlerRegistration Register(Type messageType, IContextualProvider<IMessageHandler> messageHandlerProvider);
    }

    public sealed class TypedMessageHandler<TMessage> : IMessageHandler
            where TMessage : class
    {
        private readonly IMessageHandler<TMessage> _messageHandler;

        public TypedMessageHandler(IMessageHandler<TMessage> messageHandler)
        {
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler));

            _messageHandler = messageHandler;
        }

        public ValueTask<IDispatchResult> HandleAsync(DispatchDataDictionary dispatchData, CancellationToken cancellation)
        {
            if (!(dispatchData.Message is TMessage))
                throw new InvalidOperationException($"Cannot dispatch a message of type '{dispatchData.MessageType}' to a handler that handles messages of type '{MessageType}'.");

            if (!(dispatchData is DispatchDataDictionary<TMessage> typedDispatchData))
            {
                typedDispatchData = new DispatchDataDictionary<TMessage>(dispatchData.Message as TMessage, dispatchData);
            }

            return _messageHandler.HandleAsync(typedDispatchData, cancellation);
        }

        public Type MessageType => typeof(TMessage);
    }

    public sealed class TypedMessageHandlerProvider<TMessage> : IContextualProvider<IMessageHandler>
        where TMessage : class
    {
        private readonly IContextualProvider<IMessageHandler<TMessage>> _messageHandlerProvider;

        public TypedMessageHandlerProvider(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            _messageHandlerProvider = messageHandlerProvider;
        }


        public IMessageHandler ProvideInstance(IServiceProvider serviceProvider)
        {
            var messageHandler = _messageHandlerProvider.ProvideInstance(serviceProvider);

            return new TypedMessageHandler<TMessage>(messageHandler);
        }
    }

    public sealed class TypedHandleRegistration<TMessage> : IHandlerRegistration<IMessageHandler<TMessage>>
        where TMessage : class
    {
        private readonly IHandlerRegistration _handlerRegistration;

        public TypedHandleRegistration(IContextualProvider<IMessageHandler<TMessage>> messageHandlerProvider, IHandlerRegistration handlerRegistration)
        {
            if (messageHandlerProvider == null)
                throw new ArgumentNullException(nameof(messageHandlerProvider));

            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            _handlerRegistration = handlerRegistration;
        }

        public IContextualProvider<IMessageHandler<TMessage>> Handler { get; }


        public void Dispose()
        {
            _handlerRegistration.Dispose();
        }

        public Task DisposeAsync()
        {
            return _handlerRegistration.DisposeAsync();
        }

        public Task Disposal => _handlerRegistration.Disposal;

        public Task Initialization => _handlerRegistration.Initialization;
    }
}
