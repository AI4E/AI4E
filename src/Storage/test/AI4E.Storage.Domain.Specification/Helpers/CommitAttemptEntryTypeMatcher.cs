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
using Moq;

namespace AI4E.Storage.Domain.Specification.Helpers
{
    [TypeMatcher]
    internal class CommitAttemptEntryTypeMatcher
        : ICommitAttemptEntry, IEquatable<CommitAttemptEntryTypeMatcher>, ITypeMatcher
    {
        EntityIdentifier ICommitAttemptEntry.EntityIdentifier => throw new NotSupportedException();

        CommitOperation ICommitAttemptEntry.Operation => throw new NotSupportedException();

        long ICommitAttemptEntry.Revision => throw new NotSupportedException();

        ConcurrencyToken ICommitAttemptEntry.ConcurrencyToken => throw new NotSupportedException();

        DomainEventCollection ICommitAttemptEntry.DomainEvents => throw new NotSupportedException();

        long ICommitAttemptEntry.ExpectedRevision => throw new NotSupportedException();

        object? ICommitAttemptEntry.Entity => throw new NotSupportedException();

        bool IEquatable<CommitAttemptEntryTypeMatcher>.Equals(CommitAttemptEntryTypeMatcher? other)
        {
            throw new NotSupportedException();
        }

        public bool Matches(Type typeArgument)
        {
            return typeof(ICommitAttemptEntry).IsAssignableFrom(typeArgument)
                && typeof(IEquatable<>).MakeGenericType(typeArgument).IsAssignableFrom(typeArgument);
        }
    }
}
