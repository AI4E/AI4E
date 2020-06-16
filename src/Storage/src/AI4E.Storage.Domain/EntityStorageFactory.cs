/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityStorageFactory" />
    public sealed class EntityStorageFactory : IEntityStorageFactory
    {
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Creates a new instance of the <see cref="EntityStorageFactory"/> type.
        /// </summary>
        /// <param name="serviceProvider">The <see cref="IServiceProvider"/> used to request required services.</param>
        public EntityStorageFactory(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc />
        public IEntityStorage CreateEntityStorage()
        {
            return new EntityStorage(_serviceProvider);
        }

        /// <inheritdoc />
        public IEntityStorage CreateEntityStorage(IServiceScope scope)
        {
            if (scope is null)
                throw new ArgumentNullException(nameof(scope));

            return UncheckedCreateEntityStorage(scope);
        }

        private static IEntityStorage UncheckedCreateEntityStorage(IServiceScope scope)
        {
            return ActivatorUtilities.GetServiceOrCreateInstance<IEntityStorage>(
                                scope.ServiceProvider);
        }

        private sealed class EntityStorage : IEntityStorage
        {
            private readonly IDisposable _scopeDisposer;
#pragma warning disable CA2213 // Disposing the service scope will also dispose the storage engine.
            private readonly IEntityStorage _wrappedEntityStorage;
#pragma warning restore CA2213

            public EntityStorage(IServiceProvider serviceProvider)
            {
                var scope = serviceProvider.CreateScope();
                _scopeDisposer = scope;
                _wrappedEntityStorage = UncheckedCreateEntityStorage(scope);
            }

            #region IEntityStorage

            public IEnumerable<IFoundEntityQueryResult> LoadedEntities => _wrappedEntityStorage.LoadedEntities;

            public IEntityMetadataManager MetadataManager => _wrappedEntityStorage.MetadataManager;

            public ValueTask<IEntityLoadResult> LoadEntityAsync(
                EntityIdentifier entityIdentifier,
                IDomainQueryProcessor queryProcessor,
                CancellationToken cancellation)
            {
                return _wrappedEntityStorage.LoadEntityAsync(entityIdentifier, queryProcessor, cancellation);
            }

            public IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
                Type entityType,
                CancellationToken cancellation)
            {
                return _wrappedEntityStorage.LoadEntitiesAsync(entityType, cancellation);
            }

            public ValueTask StoreAsync(
                EntityDescriptor entityDescriptor,
                CancellationToken cancellation)
            {
                return _wrappedEntityStorage.StoreAsync(entityDescriptor, cancellation);
            }

            public ValueTask DeleteAsync(
                EntityDescriptor entityDescriptor,
                CancellationToken cancellation)
            {
                return _wrappedEntityStorage.DeleteAsync(entityDescriptor, cancellation);
            }

            public ValueTask<EntityCommitResult> CommitAsync(CancellationToken cancellation)
            {
                return _wrappedEntityStorage.CommitAsync(cancellation);
            }

            public ValueTask RollbackAsync(CancellationToken cancellation)
            {
                return _wrappedEntityStorage.RollbackAsync(cancellation);
            }

            #endregion

            public void Dispose()
            {
                _scopeDisposer.Dispose();
            }
        }
    }
}
