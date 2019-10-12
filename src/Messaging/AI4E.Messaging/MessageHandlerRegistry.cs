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
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using AI4E.Utils;

namespace AI4E.Messaging
{
    /// <summary>
    /// Represents a registry where message handler can be registered.
    /// </summary>
    public sealed class MessageHandlerRegistry : IMessageHandlerRegistry
    {
        private readonly Dictionary<Type, OrderedSet<IMessageHandlerRegistration>> _handlerRegistrations;

        private readonly HashSet<IMessageHandlerRegistrationFactory> _handlerRegistrationFactories;

        /// <summary>
        /// Creates a new instance of the <see cref="IMessageHandlerRegistry"/> type.
        /// </summary>
        public MessageHandlerRegistry()
        {
            _handlerRegistrations = new Dictionary<Type, OrderedSet<IMessageHandlerRegistration>>();
            _handlerRegistrationFactories = new HashSet<IMessageHandlerRegistrationFactory>();
        }

        /// <inheritdoc />
        public bool Register(IMessageHandlerRegistration handlerRegistration)
        {
            if (handlerRegistration == null)
                throw new ArgumentNullException(nameof(handlerRegistration));

            var handlerCollection = _handlerRegistrations.GetOrAdd(handlerRegistration.MessageType, _ => new OrderedSet<IMessageHandlerRegistration>());
            var result = true;

            if (handlerCollection.Remove(handlerRegistration))
            {
                result = false; // TODO: Does this conform with spec?
            }

            handlerCollection.Add(handlerRegistration);

            return result;
        }

        /// <inheritdoc />
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

        public bool Register(IMessageHandlerRegistrationFactory handlerRegistrationFactory)
        {
            if (handlerRegistrationFactory is null)
                throw new ArgumentNullException(nameof(handlerRegistrationFactory));

            return _handlerRegistrationFactories.Add(handlerRegistrationFactory);
        }

        public bool Unregister(IMessageHandlerRegistrationFactory handlerRegistrationFactory)
        {
            if (handlerRegistrationFactory is null)
                throw new ArgumentNullException(nameof(handlerRegistrationFactory));

            return _handlerRegistrationFactories.Remove(handlerRegistrationFactory);
        }

        /// <inheritdoc />
        public IMessageHandlerProvider ToProvider()
        {
            return new MessageHandlerProvider(_handlerRegistrations, _handlerRegistrationFactories);
        }

        private sealed class MessageHandlerProvider : IMessageHandlerProvider
        {
            private readonly ImmutableDictionary<Type, ImmutableList<IMessageHandlerRegistration>> _handlerRegistrations;
            private readonly ImmutableList<IMessageHandlerRegistration> _combinedRegistrations;
            private readonly ImmutableHashSet<IMessageHandlerRegistrationFactory> _handlerRegistrationFactories;

            // Cache delegate for perf reasons.
            private readonly ConditionalWeakTable<Type, ImmutableList<IMessageHandlerRegistration>>.CreateValueCallback _buildHandlerRegistrations;
            private readonly ConditionalWeakTable<Type, ImmutableList<IMessageHandlerRegistration>> _handlerRegistrationsLookup
                = new ConditionalWeakTable<Type, ImmutableList<IMessageHandlerRegistration>>();

            public MessageHandlerProvider(
                Dictionary<Type, OrderedSet<IMessageHandlerRegistration>> handlerRegistrations,
                HashSet<IMessageHandlerRegistrationFactory> handlerRegistrationFactories)
            {
                _handlerRegistrations = handlerRegistrations.ToImmutableDictionary(
                    keySelector: kvp => kvp.Key,
                    elementSelector: kvp => kvp.Value.Reverse().ToImmutableList()); // TODO: Reverse is not very performant

                _combinedRegistrations = BuildCombinedCollection().ToImmutableList();
                _handlerRegistrationFactories = handlerRegistrationFactories.ToImmutableHashSet();

                _buildHandlerRegistrations = BuildHandlerRegistrations;
            }

            private IEnumerable<IMessageHandlerRegistration> BuildCombinedCollection()
            {
                foreach (var type in _handlerRegistrations.Keys)
                {
                    foreach (var handler in GetHandlerRegistrationsCore(type))
                    {
                        yield return handler;
                    }
                }
            }

            private ImmutableList<IMessageHandlerRegistration> GetHandlerRegistrationsCore(Type messageType)
            {
                if (!_handlerRegistrations.TryGetValue(messageType, out var result))
                {
                    return ImmutableList<IMessageHandlerRegistration>.Empty;
                }

                return result;
            }

            public IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations(Type messageType)
            {
                if (messageType is null)
                    throw new ArgumentNullException(nameof(messageType));

                return _handlerRegistrationsLookup.GetValue(messageType, _buildHandlerRegistrations);
            }

            private ImmutableList<IMessageHandlerRegistration> BuildHandlerRegistrations(Type messageType)
            {
                var handlerRegistrations = GetHandlerRegistrationsCore(messageType);

                ImmutableList<IMessageHandlerRegistration>.Builder? results = null;

                foreach (var handlerRegistrationFactory in _handlerRegistrationFactories)
                {
                    if (handlerRegistrationFactory.TryCreateMessageHandlerRegistration(
                        messageType, out var handlerRegistration))
                    {
                        if (results is null)
                        {
                            results = handlerRegistrations.ToBuilder();
                        }

                        results.Add(handlerRegistration);
                    }
                }

                if (results is null)
                {
                    return handlerRegistrations;
                }

                return results.ToImmutable();
            }

            public IReadOnlyList<IMessageHandlerRegistration> GetHandlerRegistrations()
            {
                return _combinedRegistrations;
            }
        }
    }
}
