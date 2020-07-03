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
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Storage.Domain.Test.Helpers;
using AI4E.Storage.Domain.Test.TestTypes;
using Xunit;

using CommitAttemptEntryCollection = AI4E.Storage.Domain.CommitAttemptEntryCollection<AI4E.Storage.Domain.Test.TestTypes.IEquatableCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Test
{
    public class CommitAttemptEntryCollectionTests
    {
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
            var commitAttemptEntryMock1 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
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
            var commitAttemptEntryMock1 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
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

        [Fact]
        public void ObjectEqualsOperationDoesNotEqualOtherTypeTest()
        {
            // Arrange
            var left = new CommitAttemptEntryCollection();
            var other = new object();

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.False(areEqual);
        }

        [Fact]
        public void ObjectEqualsOperationDoesNotEqualNullTest()
        {
            // Arrange
            var left = new CommitAttemptEntryCollection();
            object other = null;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.False(areEqual);
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
                var source = new CommitAttemptEntrySource();

                Add(default, default, true);
                Add(default, source.CommitAttemptEntryCollection0, true);
                Add(default, source.CommitAttemptEntryCollection1, false);
                Add(default, source.CommitAttemptEntryCollection2, false);
                Add(default, source.CommitAttemptEntryCollection3, false);
                Add(default, source.CommitAttemptEntryCollection4, false);

                Add(source.CommitAttemptEntryCollection0, default, true);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection0, true);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection1, false);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection2, false);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection3, false);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection4, false);

                Add(source.CommitAttemptEntryCollection1, default, false);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection0, false);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection1, true);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection2, true);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection3, false);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection4, false);

                Add(source.CommitAttemptEntryCollection2, default, false);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection0, false);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection1, true);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection2, true);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection3, false);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection4, false);

                Add(source.CommitAttemptEntryCollection3, default, false);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection0, false);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection1, false);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection2, false);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection3, true);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection4, false);

                Add(source.CommitAttemptEntryCollection4, default, false);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection0, false);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection1, false);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection2, false);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection3, false);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection4, true);
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
                var source = new CommitAttemptEntrySource();

                Add(default);
                Add(source.CommitAttemptEntryCollection0);
                Add(source.CommitAttemptEntryCollection1);
                Add(source.CommitAttemptEntryCollection2);
                Add(source.CommitAttemptEntryCollection3);
                Add(source.CommitAttemptEntryCollection4);
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
                var source = new CommitAttemptEntrySource();
              
                Add(default, default);
                Add(default, source.CommitAttemptEntryCollection0);
                Add(source.CommitAttemptEntryCollection0, source.CommitAttemptEntryCollection0);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection1);
                Add(source.CommitAttemptEntryCollection1, source.CommitAttemptEntryCollection2);
                Add(source.CommitAttemptEntryCollection2, source.CommitAttemptEntryCollection2);
                Add(source.CommitAttemptEntryCollection3, source.CommitAttemptEntryCollection3);
                Add(source.CommitAttemptEntryCollection4, source.CommitAttemptEntryCollection4);
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
            var commitAttemptEntryMock1 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var expectedCommitAttemptEntries = new[] { commitAttemptEntryMock1.Object, commitAttemptEntryMock2.Object }.ToImmutableArray();

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection(expectedCommitAttemptEntries);

            // Act
            var list = commitAttemptEntryCollection.ToList();

            // Assert
            Assert.Equal(expectedCommitAttemptEntries, list);
        }

        [Fact]
        public void DefaultUntypedGetEnumeratorTest()
        {
            // Arrange
            var commitAttemptEntryCollection = default(CommitAttemptEntryCollection);

            // Act
            var result = ((IEnumerable)commitAttemptEntryCollection).Cast<IEquatableCommitAttemptEntry>().Any();

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void NonGenericIterationTest()
        {
            // Arrange
            var commitAttemptEntryMock1 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var commitAttemptEntryMock2 = CommitAttemptEntrySource.SetupCommitAttemptEntryMock();
            var expectedCommitAttemptEntries = new[]
            {
                commitAttemptEntryMock1.Object,
                commitAttemptEntryMock2.Object
            }.ToImmutableArray();

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection(
                expectedCommitAttemptEntries);
            var commitAttemptEntries = new List<object?>();

            // Act
            foreach (var commitAttemptEntry in ((IEnumerable)commitAttemptEntryCollection))
            {
                commitAttemptEntries.Add(commitAttemptEntry);
            }

            // Assert
            Assert.Equal(expectedCommitAttemptEntries, commitAttemptEntries);
        }

        [Fact]
        public void DefaultValueNonGenericIterationTest()
        {
            // Arrange
            var commitAttemptEntryCollection = default(CommitAttemptEntryCollection);
            var commitAttemptEntries = new List<object?>();

            // Act
            foreach (var commitAttemptEntry in ((IEnumerable)commitAttemptEntryCollection))
            {
                commitAttemptEntries.Add(commitAttemptEntry);
            }

            // Assert
            Assert.Empty(commitAttemptEntries);
        }
    }
}
