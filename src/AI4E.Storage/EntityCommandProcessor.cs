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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Storage;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E
{
    public sealed class EntityCommandProcessor<TId, TEventBase, TEntityBase> : MessageProcessor
        where TId : struct, IEquatable<TId>
        where TEventBase : class
        where TEntityBase : class
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEntityStore<TId, TEventBase, TEntityBase> _entityStore;
        private readonly IEntityAccessor<TId, TEventBase, TEntityBase> _entityAccessor;

        public EntityCommandProcessor(IServiceProvider serviceProvider, IEntityStore<TId, TEventBase, TEntityBase> entityStore, IEntityAccessor<TId, TEventBase, TEntityBase> entityAccessor)
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
            var entityProperty = handler.GetType().GetProperties().SingleOrDefault(p => p.IsDefined<CommandHandlerEntityAttribute>());

            if (entityProperty == null ||
                !entityProperty.CanWrite ||
                !entityProperty.CanRead ||
                entityProperty.GetIndexParameters().Length > 0)
            {
                entityProperty = null;
                return await next(message);
            }

            var entityType = entityProperty.PropertyType;
            var customType = entityProperty.GetCustomAttribute<CommandHandlerEntityAttribute>().EntityType;

            if (customType != null)
            {
                if (!entityType.IsAssignableFrom(customType))
                {
                    return await next(message); // TODO: Logging
                }
                entityType = customType;
            }

            var entityStoreProperty = handler.GetType().GetProperties().SingleOrDefault(p => p.IsDefined<CommandHandlerEntityStoreAttribute>());

            if (entityStoreProperty != null &&
                entityStoreProperty.CanRead &&
                entityStoreProperty.CanWrite &&
                entityStoreProperty.GetIndexParameters().Length == 0 &&
                entityStoreProperty.PropertyType.IsAssignableFrom(_entityStore.GetType()))
            {
                entityStoreProperty.SetValue(handler, _entityStore);
            }

            var commandAccessor = _serviceProvider.GetRequiredService<ICommandAccessor<TId>>();
            var id = commandAccessor.GetEntityId(message);
            var entity = await LoadEntity(id, entityType);
            var createsEntityAttribute = Context.MessageHandlerAction.Member.GetCustomAttribute<CreatesEntityAttribute>();

            if (entity == null)
            {
                if (createsEntityAttribute == null || !createsEntityAttribute.CreatesEntity)
                    return new EntityNotFoundDispatchResult(entityType, id.ToString());
            }
            else
            {
                if (createsEntityAttribute != null && createsEntityAttribute.CreatesEntity && !createsEntityAttribute.AllowExisingEntity)
                    return new EntityAlreadyPresentDispatchResult(entityType, id.ToString());

                if (_entityAccessor.GetConcurrencyToken(entity) != commandAccessor.GetConcurrencyToken(message))
                    return new ConcurrencyIssueDispatchResult();

                entityProperty.SetValue(handler, entity);
            }

            var dispatchResult = await next(message);

            if (!dispatchResult.IsSuccess)
            {
                return dispatchResult;
            }

            var deleteFlagProperty = handler.GetType().GetProperties().SingleOrDefault(p => p.IsDefined<CommandHandlerEntityDeleteFlagAttribute>());

            var markedAsDeleted = false;

            if (deleteFlagProperty != null &&
                deleteFlagProperty.CanRead &&
                deleteFlagProperty.PropertyType == typeof(bool) &&
                deleteFlagProperty.GetIndexParameters().Length == 0)
            {
                markedAsDeleted = (bool)deleteFlagProperty.GetValue(handler);
            }

            entity = (TEntityBase)entityProperty.GetValue(handler);

            try
            {
                if (markedAsDeleted)
                {
                    await DeleteAsync(entity, entityType);
                }
                else
                {
                    await StoreAsync(entity, entityType);
                }
            }
            catch (ConcurrencyException)
            {
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
        }

        private Task StoreAsync(TEntityBase entity, Type entityType)
        {
            return _entityStore.StoreAsync(entity, entityType);
        }

        private Task DeleteAsync(TEntityBase entity, Type entityType)
        {
            return _entityStore.DeleteAsync(entity, entityType);
        }

        private Task<TEntityBase> LoadEntity(TId id, Type entityType)
        {
            return _entityStore.GetByIdAsync(id, entityType);
        }
    }
}
