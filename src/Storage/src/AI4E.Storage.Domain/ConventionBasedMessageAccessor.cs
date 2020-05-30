/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Reflection;
using AI4E.Messaging;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// A message accessor that accesses the content of messages by convention.
    /// </summary>
    public sealed class ConventionBasedMessageAccessor : IMessageAccessor
    {
        private readonly ConcurrentDictionary<Type, MessageCacheEntry> _messageCache;
        private readonly Func<Type, MessageCacheEntry> _buildMessageCacheEntry = BuildMessageCacheEntry;


        /// <summary>
        /// Creates a new instance of the <see cref="ConventionBasedMessageAccessor"/> type.
        /// </summary>
        public ConventionBasedMessageAccessor()
        {
            _messageCache = new ConcurrentDictionary<Type, MessageCacheEntry>();
        }

        static MessageCacheEntry BuildMessageCacheEntry(Type messageType)
        {
            return new MessageCacheEntry(messageType);
        }

        /// <inheritdoc/>
        public bool TryGetEntityId<TMessage>(TMessage message, out string? id)
            where TMessage : class
        {
            if (message is null)
            {
                id = null;
                return false; // TODO: Throw instead
            }

            var cacheEntry = _messageCache.GetOrAdd(typeof(TMessage), _buildMessageCacheEntry);

            if (!cacheEntry.CanReadId)
            {
                id = default;
                return false;
            }

            id = cacheEntry.ReadId(message);
            return true;
        }

        /// <inheritdoc/>
        public ConcurrencyToken GetConcurrencyToken<TMessage>(TMessage message)
            where TMessage : class
        {
            if (message is null)
            {
                return default; // TODO: Throw instead
            }

            var cacheEntry = _messageCache.GetOrAdd(typeof(TMessage), _buildMessageCacheEntry);

            if (!cacheEntry.CanReadConcurrencyToken)
            {
                return default;
            }

            return cacheEntry.ReadConcurrencyToken(message);
        }

        private readonly struct MessageCacheEntry
        {
            private readonly Func<object, string?>? _idAccessor;
            private readonly Func<object, ConcurrencyToken>? _concurrencyTokenAccessor;

            public MessageCacheEntry(Type messageType) : this()
            {
                if (messageType == null)
                    throw new ArgumentNullException(nameof(messageType));

                var messageParam = Expression.Parameter(typeof(object), "message");
                var messageConvert = Expression.Convert(messageParam, messageType);
                var idProperty = messageType.GetProperty(nameof(Command.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (idProperty != null &&
                    idProperty.CanRead &&
                    idProperty.GetIndexParameters().Length == 0)
                {
                    var idCall = Expression.MakeMemberAccess(messageConvert, idProperty);
                    var idLambda = Expression.Lambda<Func<object, string?>>(idCall.ToStringExpression(), messageParam);

                    _idAccessor = idLambda.Compile();
                }

                _concurrencyTokenAccessor = BuildConcurrencyTokenAccessor(messageType);
            }

            private static Func<object, ConcurrencyToken>? BuildConcurrencyTokenAccessor(Type messageType)
            {
                var concurrencyTokenProperty = messageType.GetProperty(
                    nameof(ConcurrencySafeCommand.ConcurrencyToken),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (concurrencyTokenProperty is null)
                {
                    return null;
                }

                if (!concurrencyTokenProperty.CanRead)
                {
                    return null;
                }

                if (concurrencyTokenProperty.GetIndexParameters().Length != 0)
                {
                    return null;
                }

                return BuildConcurrencyTokenLambda(concurrencyTokenProperty).Compile();
            }

            private static Expression<Func<object, ConcurrencyToken>> BuildConcurrencyTokenLambda(
                PropertyInfo concurrencyTokenProperty)
            {
                var messageType = concurrencyTokenProperty.ReflectedType;
                var messageParam = Expression.Parameter(typeof(object), "message");
                var messageConvert = Expression.Convert(messageParam, messageType);
                var concurrencyTokenCall = Expression.MakeMemberAccess(messageConvert, concurrencyTokenProperty);
                var concurrencyTokenAccess = BuildConcurrencyTokenAccess(concurrencyTokenCall);
                return Expression.Lambda<Func<object, ConcurrencyToken>>(
                    concurrencyTokenAccess, messageParam);
            }

            private static Expression BuildConcurrencyTokenAccess(MemberExpression concurrencyTokenCall)
            {
                if (concurrencyTokenCall.Type == typeof(ConcurrencyToken))
                {
                    return concurrencyTokenCall;
                }

                if (concurrencyTokenCall.Type == typeof(ConcurrencyToken?))
                {
                    var hasValueProperty = typeof(ConcurrencyToken?).GetProperty("HasValue");
                    var valueProperty = typeof(ConcurrencyToken?).GetProperty("Value");

                    var isNotNull = Expression.MakeMemberAccess(concurrencyTokenCall, hasValueProperty);
                    var value = Expression.MakeMemberAccess(concurrencyTokenCall, valueProperty);
                    return Expression.Condition(
                        isNotNull,
                        value,
                        Expression.Constant(default(ConcurrencyToken), typeof(ConcurrencyToken)));
                }

                return Expression.Convert(concurrencyTokenCall.ToStringExpression(), typeof(ConcurrencyToken));
            }

            public bool CanReadId => _idAccessor != null;
            public bool CanReadConcurrencyToken => _concurrencyTokenAccessor != null;

            public string? ReadId(object message)
            {
                if (message == null)
                    throw new ArgumentNullException(nameof(message));

                if (_idAccessor == null)
                    throw new InvalidOperationException();

                return _idAccessor(message);
            }

            public ConcurrencyToken ReadConcurrencyToken(object message)
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
