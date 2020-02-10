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
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Messaging.Validation;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    [CallOnValidation]
    public sealed class EntityMessageHandlerProcessor : MessageProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEntityStorageEngine _entityStorageEngine;
        private readonly IEntityPropertyAccessor _entityPropertyAccessor;
        private volatile IMessageAccessor? _messageAccessor = null;

        public EntityMessageHandlerProcessor(
            IServiceProvider serviceProvider,
            IEntityStorageEngine entityStorageEngine,
            IEntityPropertyAccessor entityPropertyAccessor)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (entityStorageEngine == null)
                throw new ArgumentNullException(nameof(entityStorageEngine));

            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            _serviceProvider = serviceProvider;
            _entityStorageEngine = entityStorageEngine;
            _entityPropertyAccessor = entityPropertyAccessor;
        }

        public override async ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next,
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            if (next is null)
                throw new ArgumentNullException(nameof(next));

            var message = dispatchData.Message;
            var handler = Context.MessageHandler;
            var descriptor = EntityMessageHandlerContextDescriptor.GetDescriptor(handler.GetType());

            if (!descriptor.IsEntityMessageHandler)
            {
                return await next(dispatchData);
            }

            if (!TryGetEntityLookup(message, descriptor, out var entityLookup))
            {
                return await next(dispatchData);
            }

            var messageAccessor = GetMessageAccessor();
            var checkConcurrencyToken = messageAccessor.TryGetConcurrencyToken(message, out var concurrencyToken);

            do
            {
                var entity = await entityLookup(cancellation);
                var createsEntityAttribute = Context.MessageHandlerAction.Member.GetCustomAttribute<CreatesEntityAttribute>();

                if (entity == null)
                {
                    if (createsEntityAttribute == null ||
                        !createsEntityAttribute.CreatesEntity)
                    {
                        return new EntityNotFoundDispatchResult(descriptor.EntityType);
                    }
                }
                else
                {
                    if (createsEntityAttribute != null &&
                        createsEntityAttribute.CreatesEntity &&
                        !createsEntityAttribute.AllowExisingEntity)
                    {
                        if (!_entityPropertyAccessor.TryGetId(descriptor.EntityType, entity, out var id))
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType);
                        }
                        else
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType, id);
                        }
                    }

                    if (checkConcurrencyToken &&
                        concurrencyToken != _entityPropertyAccessor.GetConcurrencyToken(descriptor.EntityType, entity))
                    {
                        return new ConcurrencyIssueDispatchResult();
                    }

                    descriptor.SetHandlerEntity(handler, entity);
                }

                var originalEntity = entity;
                var dispatchResult = await next(dispatchData);

                if (!dispatchResult.IsSuccess)
                {
                    return dispatchResult;
                }

                var markedAsDeleted = descriptor.IsMarkedAsDeleted(handler);
                entity = descriptor.GetHandlerEntity(handler);

                try
                {
                    var nonNullEntity = entity ?? originalEntity;

                    // The Store/Delete calls must be protected to be called with a null entity.
                    // TODO: Do we care about events etc. here?
                    if (nonNullEntity != null)
                    {
                        if (!_entityPropertyAccessor.TryGetId(descriptor.EntityType, nonNullEntity, out var id))
                        {
                            return new FailureDispatchResult("Unable to determine the id of the specified entity.");
                        }

                        if (markedAsDeleted || entity == null)
                        {
                            await _entityStorageEngine.DeleteAsync(descriptor.EntityType, nonNullEntity, id).ConfigureAwait(false);
                        }
                        else if (await _entityStorageEngine.TryStoreAsync(descriptor.EntityType, entity, id).ConfigureAwait(false))
                        {
                            dispatchResult = AddAdditionalResultData(descriptor, entity, dispatchResult);
                        }
                        else
                        {
                            continue;
                        }
                    }
                }
                catch (ConcurrencyException)
                {
                    Debug.Assert(false);
                    continue;
                }
                catch (StorageException exc)
                {
                    return new StorageIssueDispatchResult(exc);
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    return new FailureDispatchResult(exc);
                }

                return dispatchResult;

            }
            while (!checkConcurrencyToken);

            return new ConcurrencyIssueDispatchResult();
        }

        private IDispatchResult AddAdditionalResultData(
            in EntityMessageHandlerContextDescriptor descriptor,
            object entity,
            IDispatchResult dispatchResult)
        {
            var newConcurrencyToken = _entityPropertyAccessor.GetConcurrencyToken(descriptor.EntityType, entity);
            var newRevision = _entityPropertyAccessor.GetRevision(descriptor.EntityType, entity);

            var additionalResultData = new Dictionary<string, object?>
            {
                ["ConcurrencyToken"] = newConcurrencyToken,
                ["Revision"] = newRevision
            };

            return new AggregateDispatchResult(dispatchResult, additionalResultData);
        }

        private bool TryGetEntityLookup<TMessage>(
            TMessage message,
            EntityMessageHandlerContextDescriptor descriptor,
            [NotNullWhen(true)] out Func<CancellationToken, ValueTask<object?>>? entityLookup)
            where TMessage : class
        {
            var lookupAccessor = GetMatchingLookupAccessor<TMessage>(descriptor);

            if (lookupAccessor != null)
            {
                entityLookup = cancellation => lookupAccessor(Context.MessageHandler, message, _serviceProvider, cancellation);
                return true;
            }

            var messageAccessor = GetMessageAccessor();

            if (messageAccessor.TryGetEntityId(message, out var id) && id != null)
            {
                entityLookup = _ => _entityStorageEngine.GetByIdAsync(descriptor.EntityType, id);
                return true;
            }

            entityLookup = default;
            return false;
        }

        private static Func<object, object, IServiceProvider, CancellationToken, ValueTask<object?>>? GetMatchingLookupAccessor<TMessage>(
            in EntityMessageHandlerContextDescriptor descriptor)
        {
            return descriptor.GetLookupAccessor(typeof(TMessage));
        }

        private IMessageAccessor GetMessageAccessor()
        {
            if (_messageAccessor != null)
            {
                return _messageAccessor;
            }

            var messageAccessor = _serviceProvider.GetService<IMessageAccessor>();

            if (messageAccessor == null)
            {
                messageAccessor = new DefaultMessageAccessor();
            }

            return Interlocked.CompareExchange(ref _messageAccessor, messageAccessor, null) ?? messageAccessor;
        }
    }
}
