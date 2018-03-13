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
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    public sealed class EntityMessageHandlerProcessor<TId, TEventBase, TEntityBase> : MessageProcessor
        where TId : struct, IEquatable<TId>
        where TEventBase : class
        where TEntityBase : class
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEntityStore<TId, TEventBase, TEntityBase> _entityStore;
        private readonly IEntityAccessor<TId, TEventBase, TEntityBase> _entityAccessor;

        private static readonly ConcurrentDictionary<Type, HandlerCacheEntry> _handlerTypeCache = new ConcurrentDictionary<Type, HandlerCacheEntry>();

        public EntityMessageHandlerProcessor(IServiceProvider serviceProvider, IEntityStore<TId, TEventBase, TEntityBase> entityStore, IEntityAccessor<TId, TEventBase, TEntityBase> entityAccessor)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (entityStore == null)
                throw new ArgumentNullException(nameof(entityStore));

            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            _serviceProvider = serviceProvider;
            _entityStore = entityStore;
            _entityAccessor = entityAccessor;
        }

        public async override Task<IDispatchResult> ProcessAsync<TMessage>(TMessage message, Func<TMessage, Task<IDispatchResult>> next)
        {
            var handler = Context.MessageHandler;
            var cacheEntry = _handlerTypeCache.GetOrAdd(handler.GetType(), handlerType => new HandlerCacheEntry(handlerType));

            if (!cacheEntry.IsEntityMessageHandler)
            {
                return await next(message);
            }

            var messageAccessor = _serviceProvider.GetService<IMessageAccessor<TId>>() ?? new DefaultMessageAccessor<TId>();

            if (!messageAccessor.TryGetEntityId(message, out var id))
            {
                return await next(message);
            }

            var checkConcurrencyToken = messageAccessor.TryGetConcurrencyToken(message, out var concurrencyToken);

            do
            {
                var entity = await _entityStore.GetByIdAsync(cacheEntry.EntityType, id);
                var createsEntityAttribute = Context.MessageHandlerAction.Member.GetCustomAttribute<CreatesEntityAttribute>();

                if (entity == null)
                {
                    if (createsEntityAttribute == null || !createsEntityAttribute.CreatesEntity)
                        return new EntityNotFoundDispatchResult(cacheEntry.EntityType, id.ToString());
                }
                else
                {
                    if (createsEntityAttribute != null && createsEntityAttribute.CreatesEntity && !createsEntityAttribute.AllowExisingEntity)
                        return new EntityAlreadyPresentDispatchResult(cacheEntry.EntityType, id.ToString());

                    if (checkConcurrencyToken && concurrencyToken != _entityAccessor.GetConcurrencyToken(entity))
                        return new ConcurrencyIssueDispatchResult();

                    cacheEntry.SetHandlerEntity(handler, entity);
                }

                var dispatchResult = await next(message);

                if (!dispatchResult.IsSuccess)
                {
                    return dispatchResult;
                }

                var markedAsDeleted = cacheEntry.IsMarkedAsDeleted(handler);
                entity = cacheEntry.GetHandlerEntity(handler);

                try
                {
                    if (markedAsDeleted)
                    {
                        await _entityStore.DeleteAsync(cacheEntry.EntityType, entity);
                    }
                    else
                    {
                        await _entityStore.StoreAsync(cacheEntry.EntityType, entity);
                    }
                }
                catch (ConcurrencyException)
                {
                    if (!checkConcurrencyToken)
                        continue;

                    return new ConcurrencyIssueDispatchResult();
                }
                catch (StorageException exc)
                {
                    return new StorageIssueDispatchResult(exc);
                }
                catch (Exception exc)
                {
                    return new FailureDispatchResult(exc);
                }

                return dispatchResult;

            } while (true);
        }

        private readonly struct HandlerCacheEntry
        {
            private readonly Action<object, TEntityBase> _entitySetter;
            private readonly Func<object, TEntityBase> _entityGetter;
            private readonly Func<object, bool> _deleteFlagAccessor;
            private readonly Type _entityType;

            public HandlerCacheEntry(Type handlerType) : this()
            {
                var entityProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                .FirstOrDefault(p => typeof(TEntityBase).IsAssignableFrom(p.PropertyType) &&
                                                                     p.CanRead &&
                                                                     p.CanWrite &&
                                                                     p.GetIndexParameters().Length == 0 &&
                                                                     p.IsDefined<MessageHandlerEntityAttribute>());

                if (entityProperty == null)
                {
                    return;
                }

                var handlerParam = Expression.Parameter(typeof(object), "handler");
                var entityParam = Expression.Parameter(typeof(TEntityBase), "entity");
                var handlerConvert = Expression.Convert(handlerParam, handlerType);
                var entityConvert = Expression.Convert(entityParam, entityProperty.PropertyType);
                var propertyAccess = Expression.Property(handlerConvert, entityProperty);
                var propertyAssign = Expression.Assign(propertyAccess, entityConvert);
                var getterLambda = Expression.Lambda<Func<object, TEntityBase>>(propertyAccess, handlerParam);
                var setterLambda = Expression.Lambda<Action<object, TEntityBase>>(propertyAssign, handlerParam, entityParam);

                _entityGetter = getterLambda.Compile();
                _entitySetter = setterLambda.Compile();

                _entityType = entityProperty.PropertyType;
                var customType = entityProperty.GetCustomAttribute<MessageHandlerEntityAttribute>().EntityType;

                if (customType != null &&
                    _entityType.IsAssignableFrom(customType)) // If the types do not match, we just ignore the custom type.
                {
                    _entityType = customType;
                }

                var deleteFlagProperty = handlerType.GetProperties(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
                                                    .SingleOrDefault(p => p.PropertyType == typeof(bool) &&
                                                                          p.CanRead &&
                                                                          p.GetIndexParameters().Length == 0 &&
                                                                          p.IsDefined<MessageHandlerEntityDeleteFlagAttribute>());

                if (deleteFlagProperty != null)
                {
                    var deleteFlagPropertyAccess = Expression.Property(handlerConvert, deleteFlagProperty);
                    var deleteFlagLambda = Expression.Lambda<Func<object, bool>>(deleteFlagPropertyAccess, handlerParam);

                    _deleteFlagAccessor = deleteFlagLambda.Compile();
                }
            }

            public bool IsEntityMessageHandler => _entityGetter != null && _entitySetter != null;
            public Type EntityType => _entityType;

            public void SetHandlerEntity(object handler, TEntityBase entity)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_entitySetter == null)
                    throw new InvalidOperationException();

                _entitySetter(handler, entity);
            }

            public TEntityBase GetHandlerEntity(object handler)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_entityGetter == null)
                    throw new InvalidOperationException();

                return _entityGetter(handler);
            }

            public bool IsMarkedAsDeleted(object handler)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_deleteFlagAccessor == null)
                    return false;

                return _deleteFlagAccessor(handler);
            }
        }
    }
}
