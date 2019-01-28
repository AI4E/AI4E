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
        private readonly Dictionary<Type, OrderedSet<IMessageHandlerRegistration>> _handlerRegistrations;

        public MessageHandlerRegistry()
        {
            _handlerRegistrations = new Dictionary<Type, OrderedSet<IMessageHandlerRegistration>>();
        }

        public MessageHandlerRegistry(IEnumerable<IMessageHandlerRegistration> handlerRegistrations) : this()
        {
            if (handlerRegistrations == null)
                throw new ArgumentNullException(nameof(handlerRegistrations));

            foreach (var handlerRegistration in handlerRegistrations)
            {
                if (handlerRegistration == null)
                {
                    throw new ArgumentException("The collection must not contain null entries.");
                }

                Register(handlerRegistration);
            }
        }

        public bool Register(IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var handlerCollection = _handlerRegistrations.GetOrAdd(handlerRegistration.MessageType, _ => new OrderedSet<IMessageHandlerRegistration>());

            if (handlerCollection.Contains(handlerRegistration))
            {
                return false;
            }

            handlerCollection.Add(handlerRegistration);

            return true;
        }

        public bool Unregister(IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            if (!_handlerRegistrations.TryGetValue(handlerRegistration.MessageType, out var handlerCollection))
            {
                return false;
            }

            if (!handlerCollection.Remove(handlerRegistration))
            {
                return false;
            }

            if (!handlerCollection.Any())
            {
                _handlerRegistrations.Remove(handlerRegistration.MessageType);
            }

            return true;
        }

        public IMessageHandlerProvider ToProvider()
        {
            return new MessageHandlerProvider(_handlerRegistrations);
        }

        private sealed class MessageHandlerProvider : IMessageHandlerProvider
        {
            private readonly ImmutableDictionary<Type, ImmutableList<IMessageHandlerRegistration>> _handlerRegistrations;

            public MessageHandlerProvider(Dictionary<Type, OrderedSet<IMessageHandlerRegistration>> handlerRegistrations)
            {
                _handlerRegistrations = handlerRegistrations.ToImmutableDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value.Reverse().ToImmutableList()); // TODO: Reverse is not very performant
            }

            public IEnumerable<IMessageHandlerRegistration> GetHandlers(Type messageType)
            {
                if (!_handlerRegistrations.TryGetValue(messageType, out var result))
                {
                    result = ImmutableList<IMessageHandlerRegistration>.Empty;
                }

                return result;
            }

            public IEnumerable<IMessageHandlerRegistration> GetHandlers()
            {
                foreach (var type in _handlerRegistrations.Keys)
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
