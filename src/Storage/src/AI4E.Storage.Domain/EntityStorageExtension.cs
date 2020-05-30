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
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.ObjectPool;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Contains extensions for the <see cref="IEntityStorage"/> type.
    /// </summary>
    public static class EntityStorageExtension
    {
        /// <summary>
        /// Asynchronously loads the entity with the specified identifier 
        /// with the help of the current ambient query processor.
        /// </summary>
        /// <param name="entityStorage">The entity-storage.</param>
        /// <param name="entityIdentifier">The entity identifier.</param>
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
        /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
        /// </exception>
        public static ValueTask<IEntityLoadResult> LoadEntityAsync(
            this IEntityStorage entityStorage,
            EntityIdentifier entityIdentifier,
            CancellationToken cancellation = default)
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            var queryProcessor = DomainQueryProcessor.Current;
            return entityStorage.LoadEntityAsync(entityIdentifier, queryProcessor, cancellation);
        }

        private static readonly ObjectPool<ConcurrencyTokenQueryProcessor> _concurrencyTokenProcessorPool
            = ObjectPool.Create<ConcurrencyTokenQueryProcessor>();

        /// <summary>
        /// Asynchronously loads the entity with the specified identifier and checks the specified expected 
        /// concurrency-token.
        /// </summary>
        /// <param name="entityStorage">The entity-storage.</param>
        /// <param name="entityIdentifier">The entity identifier.</param>
        /// <param name="expected">
        /// The expected <see cref="ConcurrencyToken"/> or <see cref="ConcurrencyToken.NoConcurrencyToken"/> 
        /// to bypass the check.
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
        /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
        /// </exception>
        public static async ValueTask<IEntityLoadResult> LoadEntityAsync(
            this IEntityStorage entityStorage,
            EntityIdentifier entityIdentifier,
            ConcurrencyToken expected,
            CancellationToken cancellation = default)
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            using (_concurrencyTokenProcessorPool.Get(out var queryProcessor))
            {
                queryProcessor.Expected = expected;

                return await entityStorage.LoadEntityAsync(
                    entityIdentifier,
                    queryProcessor,
                    cancellation).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Asynchronously records a store operation of the specified entity.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity.</typeparam>
        /// <param name="entityStorage">The entity-storage.</param>
        /// <param name="entity">The entity to store.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The operation is not performed on the underlying storage engine, 
        /// unless <see cref="IEntityStorage.CommitAsync(CancellationToken)"/> is invoked and indicates success. 
        /// Query operations reflect the changes before the call to 
        /// <see cref="IEntityStorage.CommitAsync(CancellationToken)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
        /// </exception>
        public static ValueTask StoreAsync<TEntity>(
            this IEntityStorage entityStorage,
            TEntity entity,
            CancellationToken cancellation = default) where TEntity : class
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            return entityStorage.StoreAsync(new EntityDescriptor(typeof(TEntity), entity), cancellation);
        }

        /// <summary>
        /// Asynchronously records a delete operation of the specified entity.
        /// </summary>
        /// <typeparam name="TEntity">The type of entity.</typeparam>
        /// <param name="entityStorage">The entity-storage.</param>
        /// <param name="entity">The entity to delete.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>A <see cref="ValueTask"/> representing the asynchronous operation.</returns>
        /// <remarks>
        /// The operation is not performed on the underlying storage engine, 
        /// unless <see cref="IEntityStorage.CommitAsync(CancellationToken)"/> is invoked and indicates success. 
        /// Query operations reflect the changes before the call to 
        /// <see cref="IEntityStorage.CommitAsync(CancellationToken)"/>.
        /// </remarks>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
        /// </exception>
        public static ValueTask DeleteAsync<TEntity>(
            this IEntityStorage entityStorage,
            TEntity entity,
            CancellationToken cancellation = default) where TEntity : class
        {
            if (entityStorage is null)
                throw new ArgumentNullException(nameof(entityStorage));

            if (entity is null)
                throw new ArgumentNullException(nameof(entity));

            return entityStorage.DeleteAsync(new EntityDescriptor(typeof(TEntity), entity), cancellation);
        }
    }
}
