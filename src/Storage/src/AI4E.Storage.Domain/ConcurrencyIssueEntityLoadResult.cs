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

namespace AI4E.Storage.Domain
{
    internal sealed class ConcurrencyIssueEntityLoadResult : EntityLoadResult, IConcurrencyIssueEntityVerificationResult
    {
        public ConcurrencyIssueEntityLoadResult(EntityIdentifier entityIdentifier) : base(entityIdentifier)
        { }

        public ConcurrencyIssueEntityLoadResult(EntityIdentifier entityIdentifier, IFoundEntityQueryResult queryResult) 
            : base(entityIdentifier)
        {
            if (queryResult is null)
                throw new System.ArgumentNullException(nameof(queryResult));

            QueryResult = queryResult;
        }

        public override string Reason => Resources.NotMatchedExpectedConcurrencyToken;

        public IFoundEntityQueryResult? QueryResult { get; }

        protected override void ThrowFailure()
        {
            throw new ConcurrencyIssueEntityLoadException();
        }
    }
}