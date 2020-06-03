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
using System.Collections.Immutable;
using System.Linq;
using Moq;
using Xunit;
using CommitAttemptEntryCollection = AI4E.Storage.Domain.CommitAttemptEntryCollection<AI4E.Storage.Domain.Test.IEquatableCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Test
{
    public interface IEquatableCommitAttemptEntry : ICommitAttemptEntry, IEquatable<IEquatableCommitAttemptEntry> { }

    public class CommitAttemptEntryCollectionTests
    {
        private static Mock<IEquatableCommitAttemptEntry> SetupCommitAttemptEntryMock()
        {
            var mock = new Mock<IEquatableCommitAttemptEntry>();
            mock.Setup(commitAttemptEntry => commitAttemptEntry.Equals(It.IsAny<IEquatableCommitAttemptEntry>()))
                .Returns((IEquatableCommitAttemptEntry other) => ReferenceEquals(mock.Object, other));

            return mock;
        }

        [Fact]
        public void DefaultValueCountIsZeroTest()
        {
            // Arrange
            var commitAttemptEntryCollection = default(CommitAttemptEntryCollection);

            // Act
            var count = commitAttemptEntryCollection.Count;

            // Assert
            Assert.Equal(0, count);
        }

        [Fact]
        public void DefaultValueIterationTest()
        {
            // Arrange
            var commitAttemptEntryCollection = default(CommitAttemptEntryCollection);
            var commitAttemptEntries = new List<IEquatableCommitAttemptEntry>();

            // Act
            foreach (var commitAttemptEntry in commitAttemptEntryCollection)
            {
                commitAttemptEntries.Add(commitAttemptEntry);
            }

            // Assert
            Assert.Empty(commitAttemptEntries);
        }

        [Fact]
        public void CountTest()
        {
            // Arrange
            var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
            var commitAttemptEntries = new[] { commitAttemptEntryMock1.Object, commitAttemptEntryMock2.Object }.ToImmutableArray();

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection(
                commitAttemptEntries);

            // Act
            var count = commitAttemptEntryCollection.Count;

            // Assert
            Assert.Equal(2, count);
        }

        [Fact]
        public void IterationTest()
        {
            // Arrange
            var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
            var expectedCommitAttemptEntries = new[]
            {
                commitAttemptEntryMock1.Object,
                commitAttemptEntryMock2.Object
            }.ToImmutableArray();

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection(
                expectedCommitAttemptEntries);
            var commitAttemptEntries = new List<IEquatableCommitAttemptEntry>();

            // Act
            foreach (var commitAttemptEntry in commitAttemptEntryCollection)
            {
                commitAttemptEntries.Add(commitAttemptEntry);
            }

            // Assert
            Assert.Equal(expectedCommitAttemptEntries, commitAttemptEntries);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(
            CommitAttemptEntryCollection left,
            CommitAttemptEntryCollection right,
            bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areEqual = left == right;

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void InequalityOperationTest(
            CommitAttemptEntryCollection left,
            CommitAttemptEntryCollection right,
            bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areNotEqual = left != right;

            // Assert
            Assert.Equal(!expectedAreEqual, areNotEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualsOperationTest(
            CommitAttemptEntryCollection left,
            CommitAttemptEntryCollection right,
            bool expectedAreEqual)
        {
            // Arrange
            // -

            // Act
            var areEqual = left.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void ObjectEqualsOperationTest(
            CommitAttemptEntryCollection left,
            CommitAttemptEntryCollection right,
            bool expectedAreEqual)
        {
            // Arrange
            var other = (object)right;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EquatableEqualsOperationTest(
            CommitAttemptEntryCollection left,
            CommitAttemptEntryCollection right,
            bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<CommitAttemptEntryCollection>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData
            : TheoryData<CommitAttemptEntryCollection, CommitAttemptEntryCollection, bool>
        {
            public EqualityTestData()
            {
                var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();          
                var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
                var commitAttemptEntryMock3 = SetupCommitAttemptEntryMock();

                var commitAttemptEntryCollection0
                    = new CommitAttemptEntryCollection(ImmutableArray<IEquatableCommitAttemptEntry>.Empty);

                var commitAttemptEntryCollection1 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection2 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection3 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object,
                    commitAttemptEntryMock3.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection4 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock3.Object,
                    commitAttemptEntryMock1.Object
                }.ToImmutableArray());

                Add(default, default, true);
                Add(default, commitAttemptEntryCollection0, true);
                Add(default, commitAttemptEntryCollection1, false);
                Add(default, commitAttemptEntryCollection2, false);
                Add(default, commitAttemptEntryCollection3, false);
                Add(default, commitAttemptEntryCollection4, false);

                Add(commitAttemptEntryCollection0, default, true);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection0, true);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection1, false);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection2, false);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection3, false);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection4, false);

                Add(commitAttemptEntryCollection1, default, false);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection0, false);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection1, true);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection2, true);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection3, false);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection4, false);

                Add(commitAttemptEntryCollection2, default, false);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection0, false);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection1, true);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection2, true);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection3, false);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection4, false);

                Add(commitAttemptEntryCollection3, default, false);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection0, false);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection1, false);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection2, false);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection3, true);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection4, false);

                Add(commitAttemptEntryCollection4, default, false);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection0, false);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection1, false);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection2, false);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection3, false);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection4, true);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(CommitAttemptEntryCollection entityIdentifier)
        {
            // Arrange
            var expectedHashCode = entityIdentifier.GetHashCode();

            // Act
            var hashCode = entityIdentifier.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<CommitAttemptEntryCollection>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();
                var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
                var commitAttemptEntryMock3 = SetupCommitAttemptEntryMock();

                var commitAttemptEntryCollection0
                    = new CommitAttemptEntryCollection(ImmutableArray<IEquatableCommitAttemptEntry>.Empty);

                var commitAttemptEntryCollection1 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection2 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection3 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object,
                    commitAttemptEntryMock3.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection4 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock3.Object,
                    commitAttemptEntryMock1.Object
                }.ToImmutableArray());

                Add(default);
                Add(commitAttemptEntryCollection0);
                Add(commitAttemptEntryCollection1);
                Add(commitAttemptEntryCollection2);
                Add(commitAttemptEntryCollection3);
                Add(commitAttemptEntryCollection4);
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(CommitAttemptEntryCollection left, CommitAttemptEntryCollection right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<CommitAttemptEntryCollection, CommitAttemptEntryCollection>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();
                var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
                var commitAttemptEntryMock3 = SetupCommitAttemptEntryMock();

                var commitAttemptEntryCollection0
                    = new CommitAttemptEntryCollection(ImmutableArray<IEquatableCommitAttemptEntry>.Empty);

                var commitAttemptEntryCollection1 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection2 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection3 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock1.Object,
                    commitAttemptEntryMock2.Object,
                    commitAttemptEntryMock3.Object
                }.ToImmutableArray());

                var commitAttemptEntryCollection4 = new CommitAttemptEntryCollection(new[]
                {
                    commitAttemptEntryMock3.Object,
                    commitAttemptEntryMock1.Object
                }.ToImmutableArray());

                Add(default, default);
                Add(default, commitAttemptEntryCollection0);
                Add(commitAttemptEntryCollection0, commitAttemptEntryCollection0);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection1);
                Add(commitAttemptEntryCollection1, commitAttemptEntryCollection2);
                Add(commitAttemptEntryCollection2, commitAttemptEntryCollection2);
                Add(commitAttemptEntryCollection3, commitAttemptEntryCollection3);
                Add(commitAttemptEntryCollection4, commitAttemptEntryCollection4);
            }
        }

        [Fact]
        public void DefaultGetEnumeratorTest()
        {
            // Arrange
            var commitAttemptEntryCollection = default(CommitAttemptEntryCollection);

            // Act
            var result = commitAttemptEntryCollection.Any();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GetEnumeratorTest()
        {
            // Arrange
            var commitAttemptEntryMock1 = SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = SetupCommitAttemptEntryMock();
            var expectedCommitAttemptEntries = new[] { commitAttemptEntryMock1.Object, commitAttemptEntryMock2.Object }.ToImmutableArray();

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection(expectedCommitAttemptEntries);

            // Act
            var list = commitAttemptEntryCollection.ToList();

            // Assert
            Assert.Equal(expectedCommitAttemptEntries, list);
        }
    }
}
