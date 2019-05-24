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

namespace AI4E.Handler
{
    /// <summary>
    /// Represents the context of a message processor.
    /// </summary>
    public sealed class MessageProcessorContext : IMessageProcessorContext
    {
        private readonly Lazy<MessageHandlerConfiguration> _messageHandlerConfiguration;

        /// <summary>
        /// Creates a new instance of the <see cref="MessageProcessorContext"/> type.
        /// </summary>
        /// <param name="messageHandler">The message handler instance.</param>
        /// <param name="messageHandlerAction">A descriptor that identifies the message handler.</param>
        /// <param name="publish">A boolean value specifying whether the message is published to all handlers.</param>
        /// <param name="isLocalDispatch">A boolean value specifying whether the message is dispatched locally.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageHandler"/> is <c>null</c>.</exception>
        public MessageProcessorContext(
            object messageHandler,
            MessageHandlerActionDescriptor messageHandlerAction,
            bool publish,
            bool isLocalDispatch)
        {
            if (messageHandler == null)
                throw new ArgumentNullException(nameof(messageHandler));

            MessageHandler = messageHandler;
            MessageHandlerAction = messageHandlerAction;
            IsPublish = publish;
            IsLocalDispatch = isLocalDispatch;

            _messageHandlerConfiguration = new Lazy<MessageHandlerConfiguration>(
                () => MessageHandlerAction.BuildConfiguration(), LazyThreadSafetyMode.None);
        }

        /// <inheritdoc/>
        public MessageHandlerConfiguration MessageHandlerConfiguration => _messageHandlerConfiguration.Value;

        /// <inheritdoc/>
        public MessageHandlerActionDescriptor MessageHandlerAction { get; }

        /// <inheritdoc/>
        public object MessageHandler { get; }

        /// <inheritdoc/>
        public bool IsPublish { get; }

        /// <inheritdoc/>
        public bool IsLocalDispatch { get; }
    }
}
