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
    internal sealed class UnexpectedRevisionEntityLoadResult 
        : EntityLoadResult, IUnexpectedRevisionEntityVerificationResult
    {
        public UnexpectedRevisionEntityLoadResult(
            EntityIdentifier entityIdentifier,
            IFoundEntityQueryResult queryResult,
            ConcurrencyToken concurrencyToken,
            long revision) : base(entityIdentifier)
        {
            if (queryResult is null)
                throw new ArgumentNullException(nameof(queryResult));

            QueryResult = queryResult;
            ConcurrencyToken = concurrencyToken;
            Revision = revision;
        }

        public UnexpectedRevisionEntityLoadResult(
            EntityIdentifier entityIdentifier,
            ConcurrencyToken concurrencyToken,
            long revision) : base(entityIdentifier)
        {
            ConcurrencyToken = concurrencyToken;
            Revision = revision;
        }

        public UnexpectedRevisionEntityLoadResult(EntityIdentifier entityIdentifier) 
            : this(entityIdentifier, default, default)
        { }

        public UnexpectedRevisionEntityLoadResult(
            EntityIdentifier entityIdentifier, 
            IFoundEntityQueryResult queryResult) : this(entityIdentifier, queryResult, default, default)
        { }

        public override ConcurrencyToken ConcurrencyToken { get; }
        public override long Revision { get; }

        public IFoundEntityQueryResult? QueryResult { get; }

        public override string Reason => Resources.NotMatchedExpectedRevision;

        protected override void ThrowFailure()
        {
            throw new UnexpectedRevisionEntityLoadException();
        }
    }
}
