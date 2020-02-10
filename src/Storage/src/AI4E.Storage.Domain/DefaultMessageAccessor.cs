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
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using AI4E.Messaging;

namespace AI4E.Storage.Domain
{
    public sealed class DefaultMessageAccessor : IMessageAccessor
    {
        private readonly ConcurrentDictionary<Type, MessageCacheEntry> _messageCache;

        public DefaultMessageAccessor()
        {
            _messageCache = new ConcurrentDictionary<Type, MessageCacheEntry>();
        }

        public bool TryGetEntityId<TMessage>(TMessage message, out string? id)
        {
            if (message is null)
            {
                id = null;
                return false;
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

        public bool TryGetConcurrencyToken<TMessage>(TMessage message, out string? concurrencyToken)
        {
            if (message is null)
            {
                concurrencyToken = null;
                return false;
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
            private static readonly MethodInfo _toStringMethod = typeof(object).GetMethod(nameof(ToString))!;

            private readonly Func<object, string?> _idAccessor;
            private readonly Func<object, string?> _concurrencyTokenAccessor;

            public MessageCacheEntry(Type messageType) : this()
            {
                if (messageType == null)
                    throw new ArgumentNullException(nameof(messageType));

                var messageParam = Expression.Parameter(typeof(object), "message");
                var messageConvert = Expression.Convert(messageParam, messageType);
                var idProperty = messageType.GetProperty(nameof(Command.Id), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                var concurrencyTokenProperty = messageType.GetProperty(nameof(ConcurrencySafeCommand.ConcurrencyToken), BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

                if (idProperty != null &&
                    idProperty.CanRead &&
                    //idProperty.PropertyType == typeof(TId) &&
                    idProperty.GetIndexParameters().Length == 0)
                {
                    var idCall = Expression.MakeMemberAccess(messageConvert, idProperty);

                    Expression<Func<object, string?>> idLambda;
                    if (!idProperty.PropertyType.IsValueType)
                    {
                        var isNotNull = Expression.ReferenceNotEqual(idCall, Expression.Constant(null, idProperty.PropertyType));
                        var conversion = Expression.Call(idCall, _toStringMethod);
                        var convertedId = Expression.Condition(isNotNull, conversion, Expression.Constant(null, typeof(string)));
                        idLambda = Expression.Lambda<Func<object, string?>>(convertedId, messageParam);
                    }
                    else if (idProperty.PropertyType.IsGenericType &&
                            idProperty.PropertyType.GetGenericTypeDefinition() == typeof(Nullable<>))
                    {
                        var underlyingType = idProperty.PropertyType.GetGenericArguments().First();
                        var nullableType = typeof(Nullable<>).MakeGenericType(underlyingType);
                        var hasValueProperty = nullableType.GetProperty("HasValue");
                        var valueProperty = nullableType.GetProperty("Value");

                        var isNotNull = Expression.MakeMemberAccess(idCall, hasValueProperty);
                        var value = Expression.MakeMemberAccess(idCall, valueProperty);
                        var conversion = Expression.Call(value, _toStringMethod);
                        var convertedId = Expression.Condition(isNotNull, conversion, Expression.Constant(null, typeof(string)));
                        idLambda = Expression.Lambda<Func<object, string?>>(convertedId, messageParam);
                    }
                    else
                    {
                        var conversion = Expression.Call(idCall, _toStringMethod);
                        idLambda = Expression.Lambda<Func<object, string?>>(conversion, messageParam);
                    }

                    _idAccessor = idLambda.Compile();
                }

                if (concurrencyTokenProperty != null &&
                    concurrencyTokenProperty.CanRead &&
                    concurrencyTokenProperty.PropertyType == typeof(string) &&
                    concurrencyTokenProperty.GetIndexParameters().Length == 0)
                {
                    var concurrencyTokenCall = Expression.MakeMemberAccess(messageConvert, concurrencyTokenProperty);
                    var concurrencyTokenLambda = Expression.Lambda<Func<object, string?>>(concurrencyTokenCall, messageParam);
                    _concurrencyTokenAccessor = concurrencyTokenLambda.Compile();
                }
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

            public string? ReadConcurrencyToken(object message)
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
