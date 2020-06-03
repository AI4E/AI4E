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

using System.Collections.Immutable;
using AI4E.Storage.Domain.Test.TestTypes;
using Moq;

using CommitAttemptEntryCollection = AI4E.Storage.Domain.CommitAttemptEntryCollection<AI4E.Storage.Domain.Test.TestTypes.IEquatableCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Test.Helpers
{
    public class CommitAttemptEntrySource
    {
        public static Mock<IEquatableCommitAttemptEntry> SetupCommitAttemptEntryMock()
        {
            var mock = new Mock<IEquatableCommitAttemptEntry>();
            mock.Setup(commitAttemptEntry => commitAttemptEntry.Equals(It.IsAny<IEquatableCommitAttemptEntry>()))
                .Returns((IEquatableCommitAttemptEntry other) => ReferenceEquals(mock.Object, other));

            return mock;
        }

        public Mock<IEquatableCommitAttemptEntry> CommitAttemptEntryMock1 { get; } = SetupCommitAttemptEntryMock();
        public Mock<IEquatableCommitAttemptEntry> CommitAttemptEntryMock2 { get; } = SetupCommitAttemptEntryMock();
        public Mock<IEquatableCommitAttemptEntry> CommitAttemptEntryMock3 { get; } = SetupCommitAttemptEntryMock();

        public CommitAttemptEntryCollection CommitAttemptEntryCollection0 { get; }
                = new CommitAttemptEntryCollection(ImmutableArray<IEquatableCommitAttemptEntry>.Empty);

        public CommitAttemptEntryCollection CommitAttemptEntryCollection1 { get; }

        public CommitAttemptEntryCollection CommitAttemptEntryCollection2 { get; }

        public CommitAttemptEntryCollection CommitAttemptEntryCollection3 { get; }

        public CommitAttemptEntryCollection CommitAttemptEntryCollection4 { get; }

        public CommitAttemptEntrySource()
        {
            CommitAttemptEntryCollection1 = new CommitAttemptEntryCollection(new[]
            {
                    CommitAttemptEntryMock1.Object,
                    CommitAttemptEntryMock2.Object
            }.ToImmutableArray());

            CommitAttemptEntryCollection2 = new CommitAttemptEntryCollection(new[]
            {
                    CommitAttemptEntryMock1.Object,
                    CommitAttemptEntryMock2.Object
            }.ToImmutableArray());

            CommitAttemptEntryCollection3 = new CommitAttemptEntryCollection(new[]
            {
                    CommitAttemptEntryMock1.Object,
                    CommitAttemptEntryMock2.Object,
                    CommitAttemptEntryMock3.Object
            }.ToImmutableArray());

            CommitAttemptEntryCollection4 = new CommitAttemptEntryCollection(new[]
            {
                    CommitAttemptEntryMock3.Object,
                    CommitAttemptEntryMock1.Object
            }.ToImmutableArray());
        }
    }
}
