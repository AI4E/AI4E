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

using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a domain query-processor thats criteria is an entity's concurrency token matching an expected one.
    /// </summary>
    public sealed class ConcurrencyTokenQueryProcessor : DomainQueryProcessor
    {
        /// <inheritdoc />
        protected override bool MeetsCondition(
            ICacheableEntityLoadResult entityLoadResult,
            [NotNullWhen(false)] out IEntityLoadResult? failureLoadResult)
        {
            failureLoadResult = entityLoadResult;

            if (entityLoadResult is ISuccessEntityLoadResult)
            {
                if (!Expected.IsDefault && Expected != entityLoadResult.ConcurrencyToken)
                {
                    failureLoadResult = new ConcurrencyIssueEntityLoadResult(entityLoadResult.EntityIdentifier);
                    return false;
                }

                return true;
            }

            return false;
        }

        /// <summary>
        /// Gets or sets the expected concurrency-token.
        /// </summary>
        public ConcurrencyToken Expected { get; set; }
    }
}
