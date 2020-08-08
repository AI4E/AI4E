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

namespace AI4E.Storage.Domain
{
    /// <inheritdoc cref="IEntityVerificationResult"/>
    public class EntityVerificationResult : EntityLoadResult, IEntityVerificationResult
    {
        /// <summary>
        /// Creates a new instance of the <see cref="EntityVerificationResult"/> type.
        /// </summary>
        /// <param name="entityIdentifier">The identifier of the entity.</param>
        public EntityVerificationResult(EntityIdentifier entityIdentifier)
            : base(entityIdentifier)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityVerificationResult"/> type.
        /// </summary>
        /// <param name="queryResult">The underlying entity query-result thats verification failed.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryResult"/> is <c>null</c>.</exception>
        public EntityVerificationResult(
            IFoundEntityQueryResult queryResult)
            : this((queryResult ?? throw new ArgumentNullException(nameof(queryResult))).EntityIdentifier)
        {
            QueryResult = queryResult;
        }

        /// <inheritdoc/>
        public IFoundEntityQueryResult? QueryResult { get; }

        /// <inheritdoc/>
        public override string Reason => Resources.NotMatchedVerification;

        /// <inheritdoc/>
        public sealed override bool IsVerificationFailed(
            out EntityVerificationResult verificationEntityResult)
        {
            verificationEntityResult = this;
            return true;
        }
    }
}
