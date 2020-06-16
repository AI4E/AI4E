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
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Messaging.Validation;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// A message-processor that enabled the usage of entity managing message-handlers.
    /// </summary>
    [CallOnValidation]
    public sealed class EntityMessageHandlerProcessor : MessageProcessor
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly IEntityStorage _entityStorage;
        private readonly IMessageAccessor _messageAccessor;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityMessageHandlerProcessor"/> type.
        /// </summary>
        /// <param name="entityStorage">The entity-storage.</param>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to resolve services.</param>
        /// <param name="messageAccessor">
        /// The <see cref="IMessageAccessor"/> used to access the content of message 
        /// or <c>null</c> to used the default one.
        /// </param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="entityStorage"/> or <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        public EntityMessageHandlerProcessor(
            IEntityStorage entityStorage,
            IServiceProvider serviceProvider,
            IMessageAccessor? messageAccessor = null)
        {
            if (entityStorage == null)
                throw new ArgumentNullException(nameof(entityStorage));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
            _entityStorage = entityStorage;
            _messageAccessor = messageAccessor ?? new ConventionBasedMessageAccessor();
        }

        /// <inheritdoc/>
        public override async ValueTask<IDispatchResult> ProcessAsync<TMessage>(
            DispatchDataDictionary<TMessage> dispatchData,
            Func<DispatchDataDictionary<TMessage>, ValueTask<IDispatchResult>> next, // TODO: Is it possible to call the delegate with the user specified sync-context?
            CancellationToken cancellation)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            if (next is null)
                throw new ArgumentNullException(nameof(next));

            var message = dispatchData.Message;
            var handler = Context.MessageHandler;

            // Check whether our handler actually manages an entity as state.
            if (!EntityMessageHandlerContextDescriptor.TryGetDescriptor(handler.GetType(), out var descriptor))
            {
                return await next(dispatchData).ConfigureAwait(false);
            }

            // Try to build an entity lookup strategy based on custom entity lookups and a default fallback.
            if (!TryGetEntityLookup(message, descriptor, out var entityLookup))
            {
                return await next(dispatchData).ConfigureAwait(false);
            }

            // TODO: Perform a lookup in the DispatchDataDictionary for a key-value-pair with a key of 'ConcurrencyToken'. What shall take precedence? 

            // Try to load the concurrency token from the message.
            // We check for concurrency only if there is a concurrency token present, otherwise we just assume, that
            // all operations can be performed on the current version of the entity.
            var concurrencyToken = _messageAccessor.GetConcurrencyToken(message);

            do
            {
                var entityLoadResult = await entityLookup(concurrencyToken, cancellation).ConfigureAwait(false);

                if (entityLoadResult is IConcurrencyIssueEntityVerificationResult)
                {
                    return new ConcurrencyIssueDispatchResult();
                }

                var entity = entityLoadResult.GetEntity(throwOnFailure: false);
                var createsEntityAttribute
                    = Context.MessageHandlerAction.Member.GetCustomAttribute<CreatesEntityAttribute>();

                // The entity could not be loaded.
                if (entity is null)
                {
                    // The handler is not allowed to create entities.
                    if (createsEntityAttribute is null || !createsEntityAttribute.CreatesEntity)
                    {
                        return new EntityNotFoundDispatchResult(descriptor.EntityType);
                    }
                }
                else
                {
                    var entityDescriptor = new EntityDescriptor(descriptor.EntityType, entity);
                    var entityManager = _entityStorage.MetadataManager;

                    // The handler is allowed and forces to create entities but must not process existing entities.
                    if (createsEntityAttribute != null &&
                        createsEntityAttribute.CreatesEntity &&
                        !createsEntityAttribute.AllowExisingEntity)
                    {
                        var entityId = entityManager.GetId(entityDescriptor);

                        if (entityId is null)
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType);
                        }
                        else
                        {
                            return new EntityAlreadyPresentDispatchResult(descriptor.EntityType, entityId);
                        }
                    }

                    // Write the entity to the handler property.
                    descriptor.SetHandlerEntity(handler, entity);
                }

                var originalEntity = entity;

                // Execute the next processor (or the handler itself).
                var dispatchResult = await next(dispatchData).ConfigureAwait(false);

                if (!dispatchResult.IsSuccess)
                {
                    return dispatchResult;
                }

                var markedAsDeleted = descriptor.IsMarkedAsDeleted(handler);
                entity = descriptor.GetHandlerEntity(handler);

                try
                {
                    // The Store/Delete calls must be protected to be called with a null entity.
                    if (!markedAsDeleted && !(entity is null))
                    {
                        var entityDescriptor = new EntityDescriptor(descriptor.EntityType, entity);
                        await _entityStorage.StoreAsync(entityDescriptor, cancellation).ConfigureAwait(false);
                        dispatchResult = AddAdditionalResultData(entityDescriptor, dispatchResult);
                    }
                    else if (originalEntity != null)
                    {
                        var entityDescriptor = new EntityDescriptor(descriptor.EntityType, originalEntity);
                        await _entityStorage.DeleteAsync(entityDescriptor, cancellation).ConfigureAwait(false);
                    }

                    if (await _entityStorage.CommitAsync(cancellation).ConfigureAwait(false)
                        != EntityCommitResult.Success)
                    {
                        continue;
                    }
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
            while (concurrencyToken.IsDefault);

            return new ConcurrencyIssueDispatchResult();
        }

        private IDispatchResult AddAdditionalResultData(
            in EntityDescriptor entityDescriptor,
            IDispatchResult dispatchResult)
        {
            var entityManager = _entityStorage.MetadataManager;
            var newConcurrencyToken = entityManager.GetConcurrencyToken(entityDescriptor);
            var newRevision = entityManager.GetRevision(entityDescriptor);

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
            [NotNullWhen(true)] out EntityLookup? entityLookup)
            where TMessage : class
        {
            // If we have a lookup accessor, that is a user-defined entity lookup, use this as entity lookup strategy.
            if (descriptor.TryGetLookupAccessor(typeof(TMessage), out var lookupAccessor))
            {
                async ValueTask<IEntityLoadResult> ExecuteLookupAccessor(
                    ConcurrencyToken concurrencyToken, CancellationToken cancellation)
                {
                    var entityLoadResult = await lookupAccessor!.Invoke(
                        Context.MessageHandler, message, _serviceProvider, cancellation).ConfigureAwait(false);

                    if (entityLoadResult is IFoundEntityQueryResult
                        && concurrencyToken != default
                        && concurrencyToken != entityLoadResult.ConcurrencyToken)
                    {
                        return new ConcurrencyIssueEntityLoadResult(entityLoadResult.EntityIdentifier);
                    }

                    return entityLoadResult;
                }

                entityLookup = ExecuteLookupAccessor;
                return true;
            }

            // TODO: Perform a lookup in the DispatchDataDictionary for a key-value-pair with a key of 'Id'. What shall take precedence? 

            // If we can't lookup the entity id, there is not way to load the entity.
            if (!_messageAccessor.TryGetEntityId(message, out var entityId) || entityId == null)
            {
                entityLookup = default;
                return false;
            }

            ValueTask<IEntityLoadResult> ExecuteLoadFromStorage(
                ConcurrencyToken concurrencyToken, CancellationToken cancellation)
            {
                var entityIdentifier = new EntityIdentifier(descriptor.EntityType, entityId!);
                return _entityStorage.LoadEntityAsync(entityIdentifier, concurrencyToken, cancellation);
            }

            entityLookup = ExecuteLoadFromStorage;
            return true;
        }

        private delegate ValueTask<IEntityLoadResult> EntityLookup(
            ConcurrencyToken expectedConcurrencyToken,
            CancellationToken cancellation);
    }
}
