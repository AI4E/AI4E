/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IMessageHandler.cs
 * Types:           (1) AI4E.MessageHandlerRegistry
 *                  (2) AI4E.MessageHandlerRegistry.MessageHandlerProvider
 * Version:         1.0
 * Author:          Andreas Trütschel
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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Utils;

namespace AI4E
{
    public sealed class MessageHandlerRegistry : IMessageHandlerRegistry
    {
        private readonly Dictionary<Type, OrderedSet<IMessageHandlerFactory>> _handlers;

        public MessageHandlerRegistry()
        {
            _handlers = new Dictionary<Type, OrderedSet<IMessageHandlerFactory>>();
        }

        public bool Register(IMessageHandlerFactory messageHandlerFactory)
        {
            if (messageHandlerFactory == null)
                throw new ArgumentNullException(nameof(messageHandlerFactory));

            var handlerCollection = _handlers.GetOrAdd(messageHandlerFactory.MessageType, _ => new OrderedSet<IMessageHandlerFactory>());

            if (handlerCollection.Contains(messageHandlerFactory))
            {
                return false;
            }

            handlerCollection.Add(messageHandlerFactory);

            return true;
        }

        public bool Unregister(IMessageHandlerFactory messageHandlerFactory)
        {
            if (messageHandlerFactory == null)
                throw new ArgumentNullException(nameof(messageHandlerFactory));

            if (!_handlers.TryGetValue(messageHandlerFactory.MessageType, out var handlerCollection))
            {
                return false;
            }

            if (!handlerCollection.Remove(messageHandlerFactory))
            {
                return false;
            }

            if (!handlerCollection.Any())
            {
                _handlers.Remove(messageHandlerFactory.MessageType);
            }

            return true;
        }

        public IMessageHandlerProvider ToProvider()
        {
            return new MessageHandlerProvider(_handlers);
        }

        private sealed class MessageHandlerProvider : IMessageHandlerProvider
        {
            private readonly ImmutableDictionary<Type, ImmutableList<IMessageHandlerFactory>> _handlers;

            public MessageHandlerProvider(Dictionary<Type, OrderedSet<IMessageHandlerFactory>> handlers)
            {
                _handlers = handlers.ToImmutableDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value.Reverse().ToImmutableList()); // TODO: Reverse is not very performant
            }

            public IEnumerable<IMessageHandlerFactory> GetHandlers(Type messageType)
            {
                if (!_handlers.TryGetValue(messageType, out var result))
                {
                    result = ImmutableList<IMessageHandlerFactory>.Empty;
                }

                return result;
            }

            public IEnumerable<IMessageHandlerFactory> GetHandlers()
            {
                foreach (var type in _handlers.Keys)
                {
                    foreach (var handler in GetHandlers(type))
                    {
                        yield return handler;
                    }
                }
            }
        }
    }
}
