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

            var messageAccessor = _serviceProvider.GetRequiredService<IMessageAccessor<TId>>() ?? new DefaultMessageAccessor<TId>();

            if (!messageAccessor.TryGetEntityId(message, out var id))
            {
                return await next(message);
            }

            var checkConcurrencyToken = messageAccessor.TryGetConcurrencyToken(message, out var concurrencyToken);

            do
            {
                var entity = await _entityStore.GetByIdAsync(id, cacheEntry.EntityType);
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
                        await _entityStore.DeleteAsync(entity, cacheEntry.EntityType);
                    }
                    else
                    {
                        await _entityStore.StoreAsync(entity, cacheEntry.EntityType);
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
            private readonly PropertyInfo _entityProperty;
            private readonly Type _entityType;

            private readonly PropertyInfo _deleteFlagProperty;

            public HandlerCacheEntry(Type handlerType)
            {
                _entityProperty = handlerType.GetProperties().SingleOrDefault(p => p.IsDefined<MessageHandlerEntityAttribute>());

                if (_entityProperty == null ||
                    !_entityProperty.CanWrite ||
                    !_entityProperty.CanRead ||
                    !typeof(TEntityBase).IsAssignableFrom(_entityProperty.PropertyType) ||
                    _entityProperty.GetIndexParameters().Length > 0)
                {
                    _entityProperty = null;
                }

                _entityType = _entityProperty.PropertyType;
                var customType = _entityProperty.GetCustomAttribute<MessageHandlerEntityAttribute>().EntityType;

                if (customType != null &&
                    _entityType.IsAssignableFrom(customType)) // If the types do not match, we just ignore the custom type.
                {
                    _entityType = customType;
                }

                _deleteFlagProperty = handlerType.GetProperties().SingleOrDefault(p => p.IsDefined<MessageHandlerEntityDeleteFlagAttribute>());

                if (_deleteFlagProperty == null ||
                    !_deleteFlagProperty.CanRead ||
                    _deleteFlagProperty.PropertyType != typeof(bool) ||
                    _deleteFlagProperty.GetIndexParameters().Length > 0)
                {
                    _deleteFlagProperty = null;
                }
            }

            public bool IsEntityMessageHandler => _entityProperty != null;
            public Type EntityType => _entityType;
            public PropertyInfo EntityProperty => _entityProperty;

            public void SetHandlerEntity(object handler, TEntityBase entity)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_entityProperty == null)
                    throw new InvalidOperationException();

                _entityProperty.SetValue(handler, entity);
            }

            public TEntityBase GetHandlerEntity(object handler)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_entityProperty == null)
                    throw new InvalidOperationException();

                return (TEntityBase)_entityProperty.GetValue(handler);
            }

            public bool IsMarkedAsDeleted(object handler)
            {
                if (handler == null)
                    throw new ArgumentNullException(nameof(handler));

                if (_deleteFlagProperty == null)
                    return false;

                return (bool)_deleteFlagProperty.GetValue(handler);
            }
        }
    }
}
