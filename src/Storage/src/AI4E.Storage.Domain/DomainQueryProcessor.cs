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
using System.Diagnostics.CodeAnalysis;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// A base class for custom domain query-processor implementations.
    /// </summary>
    public abstract class DomainQueryProcessor : IDomainQueryProcessor
    {
        /// <inheritdoc/>
        public virtual async ValueTask<IEntityLoadResult> ProcessAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryExecutor executor,
            CancellationToken cancellation = default)
        {
            if (executor is null)
                throw new ArgumentNullException(nameof(executor));

            // Load with caching enabled.
            var entityLoadResult = await executor.ExecuteAsync(entityIdentifier, bypassCache: false, cancellation)
                .ConfigureAwait(false);

            // If the processor condition is met, return the result.
            if (MeetsCondition(entityLoadResult, out var failureLoadResult))
            {
                return entityLoadResult;
            }

            // When the result was freshly loaded, we do not need to reload with caching disabled, 
            // just return the failure result.
            if (!entityLoadResult.LoadedFromCache)
            {
                return failureLoadResult;
            }

            // Load again with caching disabled.
            entityLoadResult = await executor.ExecuteAsync(entityIdentifier, bypassCache: true, cancellation)
                .ConfigureAwait(false);

            // Check the processor condition again.
            if (MeetsCondition(entityLoadResult, out failureLoadResult))
            {
                return entityLoadResult;
            }

            return failureLoadResult;
        }

        /// <summary>
        /// When overridden in a derived class indicates whether the specified entity load-result meets the 
        /// domain query-processor's conditions.
        /// </summary>
        /// <param name="entityLoadResult">The entity load-result.</param>
        /// <param name="failureLoadResult">
        /// Contains the failure entity load-result that is used when domain query-processor's condition is not met.
        /// </param>
        /// <returns>True if the domain query-processors are met, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityLoadResult"/> is <c>null</c>.
        /// </exception>
        protected abstract bool MeetsCondition(
            IEntityQueryResult entityLoadResult,
            [NotNullWhen(false)] out IEntityLoadResult? failureLoadResult);

        /// <summary>
        /// Gets the instance of the default query processor thats success condition is a success entity load-result.
        /// </summary>
        public static DomainQueryProcessor Default { get; } = new DefaultDomainQueryProcessor();

        private static readonly AsyncLocal<DomainQueryProcessor?> _current = new AsyncLocal<DomainQueryProcessor?>();

        /// <summary>
        /// Gets the current ambient domain query-processor.
        /// </summary>
        public static DomainQueryProcessor Current => _current.Value ?? Default;

        /// <summary>
        /// Sets the current ambient domain query-processor.
        /// </summary>
        /// <param name="queryProcessor">The desired ambient domain query-processor.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="queryProcessor"/> is <c>null</c>.
        /// </exception>
        public static void SetCurrent(DomainQueryProcessor queryProcessor)
        {
            if (queryProcessor is null)
                throw new ArgumentNullException(nameof(queryProcessor));

            _current.Value = queryProcessor;
        }

        /// <summary>
        /// Resets the current ambient domain query-processor to the default domain query-processor.
        /// </summary>
        public static void ResetCurrent()
        {
            _current.Value = null;
        }

        private sealed class DefaultDomainQueryProcessor : DomainQueryProcessor
        {
            public DefaultDomainQueryProcessor() { }

            protected override bool MeetsCondition(
                IEntityQueryResult entityLoadResult,
                [NotNullWhen(false)] out IEntityLoadResult? failureLoadResult)
            {
                failureLoadResult = entityLoadResult;
                return entityLoadResult is IFoundEntityQueryResult;
            }
        }
    }
}
