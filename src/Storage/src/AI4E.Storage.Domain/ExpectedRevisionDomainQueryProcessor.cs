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
    /// Represents a domain query-processor thats criteria is an entity's revision being in an expected range.
    /// </summary>
    public sealed class ExpectedRevisionDomainQueryProcessor : DomainQueryProcessor
    {
        private long? _minExpectedRevision;
        private long? _maxExpectedRevision;

        /// <inheritdoc/>
        public override ValueTask<IEntityLoadResult> ProcessAsync(
            EntityIdentifier entityIdentifier,
            IDomainQueryExecutor executor,
            CancellationToken cancellation = default)
        {
            // This can never succeed. Just short circuit here.
            if (_minExpectedRevision > _maxExpectedRevision)
            {
                return new ValueTask<IEntityLoadResult>(
                    new UnexpectedRevisionEntityVerificationResult(entityIdentifier));
            }

            return base.ProcessAsync(entityIdentifier, executor, cancellation);
        }

        /// <inheritdoc />
        protected override bool MeetsCondition(
            IEntityQueryResult entityQueryResult,
            [NotNullWhen(false)] out IEntityLoadResult? failureLoadResult)
        {
            failureLoadResult = entityQueryResult;

            if (entityQueryResult.IsFound(out var foundEntityQueryResult))
            {
                if (MinExpectedRevision.HasValue && foundEntityQueryResult.Revision < MinExpectedRevision
                    || MaxExpectedRevision.HasValue && foundEntityQueryResult.Revision > MaxExpectedRevision)
                {
                    failureLoadResult = new UnexpectedRevisionEntityVerificationResult(foundEntityQueryResult);

                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets or sets the minimum expected entity revision or <c>null</c> to bypass the check.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified value is negative.</exception>
        public long? MinExpectedRevision
        {
            get => _minExpectedRevision;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(Resources.ValueMustNotBeNegative);

                _minExpectedRevision = value;
            }
        }

        /// <summary>
        /// Gets or sets the maximum expected entity revision or <c>null</c> to bypass the check.
        /// </summary>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if the specified value is negative.</exception>
        public long? MaxExpectedRevision
        {
            get => _maxExpectedRevision;
            set
            {
                if (value < 0)
                    throw new ArgumentOutOfRangeException(Resources.ValueMustNotBeNegative);

                _maxExpectedRevision = value;
            }
        }
    }
}
