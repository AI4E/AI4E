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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a specialized entity storage that allows querying entities as well as modifying them.
    /// </summary>
    public interface IEntityStorage : IDisposable
    {
        /// <summary>
        /// Gets a collection of all loaded entities.
        /// </summary>
        IEnumerable<IFoundEntityQueryResult> LoadedEntities { get; }

        /// <summary>
        /// Gets the meta-data manager that is used to get and set entity meta-data to entities.
        /// </summary>
        IEntityMetadataManager MetadataManager { get; }

        /// <summary>
        /// Asynchronously loads the entity with the specified identifier 
        /// with the help of the specified query processor.
        /// </summary>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="queryProcessor">
        /// The <see cref="IDomainQueryProcessor"/> that is used to process the query.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityLoadResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entity load-result that describes the load operation status 
        /// and contains the entity on success.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="queryProcessor"/> is <c>null</c>.
        /// </exception>
        ValueTask<IEntityLoadResult> LoadEntityAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryProcessor queryProcessor,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously loads all entity of the specified type.
        /// </summary>
        /// <param name="entityType">The type of entity to load.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="IAsyncEnumerable{ISuccessEntityLoadResult}"/> asynchronously enumerating the entity 
        /// load-results of all entity of type <paramref name="entityType"/> that are available.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityType"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="entityType"/> specifies a delegate type, a value-type,
        /// an interface type or an open generic type definition.
        /// </exception>
        IAsyncEnumerable<IFoundEntityQueryResult> LoadEntitiesAsync(
            Type entityType,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously records a store operation of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">An <see cref="EntityDescriptor"/> describing the entity to store.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The operation is not performed on the underlying storage engine, 
        /// unless <see cref="CommitAsync(CancellationToken)"/> is invoked and indicates success. 
        /// Query operations reflect the changes before the call to <see cref="CommitAsync(CancellationToken)"/>.
        /// </remarks>
        ValueTask StoreAsync(
            EntityDescriptor entityDescriptor,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously records a delete operation of the specified entity.
        /// </summary>
        /// <param name="entityDescriptor">An <see cref="EntityDescriptor"/> describing the entity to delete.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The operation is not performed on the underlying storage engine,
        /// unless <see cref="CommitAsync(CancellationToken)"/> is invoked and indicates success. 
        /// Query operations reflect the changes before the call to <see cref="CommitAsync(CancellationToken)"/>.
        /// </remarks>
        ValueTask DeleteAsync(
            EntityDescriptor entityDescriptor,
            CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously commits all recorded operations and resets the storage.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityLoadResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the commit result.
        /// </returns>
        /// <remarks>
        /// Regardless of the commit result, the storage is reset to a state as it had just been created, that means,
        /// there are no recorded operations and no loaded entities.
        /// </remarks>
        ValueTask<EntityCommitResult> CommitAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Rolls back all recorded operations and resets the storage.
        /// </summary>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The storage is reset to a state as it had just been created, that means,
        /// there are no recorded operations and no loaded entities.
        /// </remarks>
        ValueTask RollbackAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Represents a domain query processor that processes domain queries.
    /// </summary>
    public interface IDomainQueryProcessor
    {
        /// <summary>
        /// Asynchronously processes a query of the entity with the specified identifier.
        /// </summary>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="executor">
        /// The <see cref="IDomainQueryExecutor"/> that executed queries on the underlying storage engine.
        /// </param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityLoadResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entity load-result that describes the load operation status 
        /// and contains the entity on success.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="executor"/> is <c>null</c>.</exception>
        ValueTask<IEntityLoadResult> ProcessAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryExecutor executor,
            CancellationToken cancellation = default);
    }

    /// <summary>
    /// Represents a query executor that executed queries on the underlying storage engine.
    /// </summary>
    public interface IDomainQueryExecutor
    {
        /// <summary>
        /// Asynchronously loads the entity with the specified identifier.
        /// </summary>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="bypassCache">A boolean value indicating whether the internal caches shall be by-passed.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{IEntityQueryResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the entity load-result that describes the load operation status 
        /// and contains the entity on success.
        /// </returns>
        ValueTask<IEntityQueryResult> ExecuteAsync(
            EntityIdentifier entityIdentifier,
            bool bypassCache,
            CancellationToken cancellation = default);
    }
}
