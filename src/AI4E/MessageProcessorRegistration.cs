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
using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    /// <summary>
    /// Represents the registration of a message processor.
    /// </summary>
    public sealed class MessageProcessorRegistration : IMessageProcessorRegistration
    {
        private readonly Func<IServiceProvider, IMessageProcessor> _factory;

        private MessageProcessorRegistration(Type messageProcessorType, Func<IServiceProvider, IMessageProcessor> factory)
        {
            MessageProcessorType = messageProcessorType;
            _factory = factory;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageProcessorRegistration"/> type.
        /// </summary>
        /// <param name="messageProcessor">The message processor instance..</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageProcessor"/> is null.</exception>
        public MessageProcessorRegistration(IMessageProcessor messageProcessor)
        {
            if (messageProcessor == null)
                throw new ArgumentNullException(nameof(messageProcessor));

            MessageProcessorType = messageProcessor.GetType();
            _factory = _ => messageProcessor;
        }

        private MessageProcessorRegistration(Type messageProcessorType)
        {
            Debug.Assert(messageProcessorType != null);
            Debug.Assert(typeof(IMessageProcessor).IsAssignableFrom(messageProcessorType));

            MessageProcessorType = messageProcessorType;
            _factory = serviceProvider => (IMessageProcessor)ActivatorUtilities.CreateInstance(serviceProvider, messageProcessorType);
        }

        /// <inheritdoc />
        public IMessageProcessor CreateMessageProcessor(IServiceProvider serviceProvider)
        {
            return _factory(serviceProvider);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageProcessorRegistration"/> type.
        /// </summary>
        /// <typeparam name="TProcessor">The type of message processor.</typeparam>
        /// <returns>The created <see cref="MessageProcessorRegistration"/>.</returns>
        public static MessageProcessorRegistration Create<TProcessor>()
            where TProcessor : class, IMessageProcessor
        {
            return new MessageProcessorRegistration(typeof(TProcessor));
        }

        /// <summary>
        /// Creates a new instance of the <see cref="MessageProcessorRegistration"/> type.
        /// </summary>
        /// <typeparam name="TProcessor">The type of message processor.</typeparam>
        /// <param name="factory">A factory that is used to create message processors.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is null.</exception>
        public static MessageProcessorRegistration Create<TProcessor>(Func<IServiceProvider, TProcessor> factory)
            where TProcessor : class, IMessageProcessor
        {
            return new MessageProcessorRegistration(typeof(TProcessor), factory);
        }

        /// <inheritdoc />
        public Type MessageProcessorType { get; }
    }
}
