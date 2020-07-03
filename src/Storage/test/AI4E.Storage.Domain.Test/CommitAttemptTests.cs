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
using AI4E.Storage.Domain.Test.Helpers;
using Xunit;

using CommitAttempt = AI4E.Storage.Domain.CommitAttempt<AI4E.Storage.Domain.Test.TestTypes.IEquatableCommitAttemptEntry>;

namespace AI4E.Storage.Domain.Test
{
    public class CommitAttemptTests
    {

        [Fact]
        public void DefaultValueEntriesTestTest()
        {
            // Arrange
            var commitAttempt = default(CommitAttempt);

            // Act
            var entries = commitAttempt.Entries;

            // Assert
            Assert.Equal(default, entries);
        }

        [Fact]
        public void EntriesTest()
        {
            // Arrange
            var source = new CommitAttemptEntrySource();
            var expectedEntries = source.CommitAttemptEntryCollection1;
            var commitAttempt = new CommitAttempt(expectedEntries);

            // Act
            var entries = commitAttempt.Entries;

            // Assert
            Assert.Equal(expectedEntries, entries);

        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(CommitAttempt left, CommitAttempt right, bool expectedAreEqual)
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
        public void InequalityOperationTest(CommitAttempt left, CommitAttempt right, bool expectedAreEqual)
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
        public void EqualsOperationTest(CommitAttempt left, CommitAttempt right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(CommitAttempt left, CommitAttempt right, bool expectedAreEqual)
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
            var left = new CommitAttempt();
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
            var left = new CommitAttempt();
            object other = null;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.False(areEqual);
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EquatableEqualsOperationTest(CommitAttempt left, CommitAttempt right, bool expectedAreEqual)
        {
            // Arrange
            var equatable = (IEquatable<CommitAttempt>)left;

            // Act
            var areEqual = equatable.Equals(right);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<CommitAttempt, CommitAttempt, bool>
        {
            public EqualityTestData()
            {
                var source = new CommitAttemptEntrySource();
                var commitAttempt0 = new CommitAttempt(source.CommitAttemptEntryCollection0);
                var commitAttempt1 = new CommitAttempt(source.CommitAttemptEntryCollection1);
                var commitAttempt2 = new CommitAttempt(source.CommitAttemptEntryCollection3);
                var commitAttempt3 = new CommitAttempt(source.CommitAttemptEntryCollection4);

                Add(default, default, true);
                Add(default, commitAttempt0, true);
                Add(default, commitAttempt1, false);
                Add(default, commitAttempt2, false);
                Add(default, commitAttempt3, false);

                Add(commitAttempt0, default, true);
                Add(commitAttempt0, commitAttempt0, true);
                Add(commitAttempt0, commitAttempt1, false);
                Add(commitAttempt0, commitAttempt2, false);
                Add(commitAttempt0, commitAttempt3, false);

                Add(commitAttempt1, default, false);
                Add(commitAttempt1, commitAttempt0, false);
                Add(commitAttempt1, commitAttempt1, true);
                Add(commitAttempt1, commitAttempt2, false);
                Add(commitAttempt1, commitAttempt3, false);

                Add(commitAttempt2, default, false);
                Add(commitAttempt2, commitAttempt0, false);
                Add(commitAttempt2, commitAttempt1, false);
                Add(commitAttempt2, commitAttempt2, true);
                Add(commitAttempt2, commitAttempt3, false);

                Add(commitAttempt3, default, false);
                Add(commitAttempt3, commitAttempt0, false);
                Add(commitAttempt3, commitAttempt1, false);
                Add(commitAttempt3, commitAttempt2, false);
                Add(commitAttempt3, commitAttempt3, true);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(CommitAttempt entityIdentifier)
        {
            // Arrange
            var expectedHashCode = entityIdentifier.GetHashCode();

            // Act
            var hashCode = entityIdentifier.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<CommitAttempt>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                var source = new CommitAttemptEntrySource();
                var commitAttempt0 = new CommitAttempt(source.CommitAttemptEntryCollection0);
                var commitAttempt1 = new CommitAttempt(source.CommitAttemptEntryCollection1);
                var commitAttempt2 = new CommitAttempt(source.CommitAttemptEntryCollection3);
                var commitAttempt3 = new CommitAttempt(source.CommitAttemptEntryCollection4);

                Add(default);
                Add(commitAttempt0);
                Add(commitAttempt1);
                Add(commitAttempt2);
                Add(commitAttempt3);
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(CommitAttempt left, CommitAttempt right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<CommitAttempt, CommitAttempt>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                var source = new CommitAttemptEntrySource();
                var commitAttempt0 = new CommitAttempt(source.CommitAttemptEntryCollection0);
                var commitAttempt1 = new CommitAttempt(source.CommitAttemptEntryCollection1);
                var commitAttempt2 = new CommitAttempt(source.CommitAttemptEntryCollection3);
                var commitAttempt3 = new CommitAttempt(source.CommitAttemptEntryCollection4);

                Add(default, default);
                Add(default, commitAttempt0);
                Add(commitAttempt0, commitAttempt0);
                Add(commitAttempt1, commitAttempt1);
                Add(commitAttempt2, commitAttempt2);
                Add(commitAttempt3, commitAttempt3);
            }
        }
    }
}
