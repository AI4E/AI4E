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
using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace AI4E
{
    public sealed class DefaultMessageAccessor<TId> : IMessageAccessor<TId>
        where TId : struct, IEquatable<TId>
    {
        private static readonly ConcurrentDictionary<Type, MessageCacheEntry> _messageCache = new ConcurrentDictionary<Type, MessageCacheEntry>();

        public DefaultMessageAccessor() { }

        public bool TryGetEntityId<TMessage>(TMessage message, out TId id)
        {
            if (message is Command<TId> command)
            {
                id = command.Id;
                return true;
            }

            var cacheEntry = _messageCache.GetOrAdd(typeof(TMessage), messageType => new MessageCacheEntry(messageType));

            if (!cacheEntry.CanReadId)
            {
                id = default;
                return false;
            }

            id = cacheEntry.ReadId(message);
            return true;
        }

        public bool TryGetConcurrencyToken<TMessage>(TMessage message, out Guid concurrencyToken)
        {
            if (message is Command<TId> command)
            {
                concurrencyToken = command.ConcurrencyToken;
                return true;
            }

            var cacheEntry = _messageCache.GetOrAdd(typeof(TMessage), messageType => new MessageCacheEntry(messageType));

            if (!cacheEntry.CanReadConcurrencyToken)
            {
                concurrencyToken = default;
                return false;
            }

            concurrencyToken = cacheEntry.ReadConcurrencyToken(message);
            return true;
        }

        private readonly struct MessageCacheEntry
        {
            private readonly Func<object, TId> _idAccessor;
            private readonly Func<object, Guid> _concurrencyTokenAccessor;

            public MessageCacheEntry(Type messageType) : this()
            {
                if (messageType == null)
                    throw new ArgumentNullException(nameof(messageType));

                var messageParam = Expression.Parameter(typeof(object), "message");
                var messageConvert = Expression.Convert(messageParam, messageType);
                var idProperty = messageType.GetProperty(nameof(Command<TId>.Id));
                var concurrencyTokenProperty = messageType.GetProperty(nameof(Command<TId>.ConcurrencyToken));

                if (idProperty != null &&
                    idProperty.CanRead &&
                    idProperty.PropertyType == typeof(TId) &&
                    idProperty.GetIndexParameters().Length == 0)
                {
                    var idCall = Expression.MakeMemberAccess(messageConvert, idProperty);
                    var idLambda = Expression.Lambda<Func<object, TId>>(idCall, messageParam);
                    _idAccessor = idLambda.Compile();
                }

                if (concurrencyTokenProperty != null &&
                    concurrencyTokenProperty.CanRead &&
                    concurrencyTokenProperty.PropertyType == typeof(Guid) &&
                    concurrencyTokenProperty.GetIndexParameters().Length == 0)
                {
                    var concurrencyTokenCall = Expression.MakeMemberAccess(messageConvert, concurrencyTokenProperty);
                    var concurrencyTokenLambda = Expression.Lambda<Func<object, Guid>>(concurrencyTokenCall, messageParam);
                    _concurrencyTokenAccessor = concurrencyTokenLambda.Compile();
                }
            }

            public bool CanReadId => _idAccessor != null;
            public bool CanReadConcurrencyToken => _concurrencyTokenAccessor != null;

            public TId ReadId(object message)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (_idAccessor == null)
                    throw new InvalidOperationException();

                return _idAccessor(message);
            }

            public Guid ReadConcurrencyToken(object message)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (_concurrencyTokenAccessor == null)
                    throw new InvalidOperationException();

                return _concurrencyTokenAccessor(message);
            }
        }
    }
}
