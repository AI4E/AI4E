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
using Xunit;

namespace AI4E.Storage.Domain.Test
{
    public class ConcurrencyTokenTests
    {
        private static readonly ConcurrencyToken _concurrencyToken0 = new ConcurrencyToken(string.Empty);
        private static readonly ConcurrencyToken _concurrencyToken1 = new ConcurrencyToken("sokvm");
        private static readonly ConcurrencyToken _concurrencyToken2 = new ConcurrencyToken("  ");

        [Fact]
        public void ConstructNullRawValueThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("rawValue", () =>
            {
                new ConcurrencyToken(null);
            });
        }

        [Fact]
        public void RawValueIsCorrectlySetTest()
        {
            // Arrange
            var expectedRawValue = "abc";
            var concurrencyToken = new ConcurrencyToken(expectedRawValue);

            // Act
            var rawValue = concurrencyToken.RawValue;

            // Assert
            Assert.Equal(expectedRawValue, rawValue);
        }

        [Fact]
        public void DefaultValueRawValueIsEmptyStringTest()
        {
            // Arrange
            var concurrencyToken = default(ConcurrencyToken);

            // Act
            var rawValue = concurrencyToken.RawValue;

            // Assert
            Assert.Equal(string.Empty, rawValue);
        }

        [Fact]
        public void ToStringTest()
        {
            // Arrange
            var expectedRawValue = "abc";
            var concurrencyToken = new ConcurrencyToken(expectedRawValue);

            // Act
            var rawValue = concurrencyToken.ToString();

            // Assert
            Assert.Equal(expectedRawValue, rawValue);
        }

        [Fact]
        public void DefaultValueToStringReturnsEmptyStringTest()
        {
            // Arrange
            var concurrencyToken = default(ConcurrencyToken);

            // Act
            var rawValue = concurrencyToken.ToString();

            // Assert
            Assert.Equal(string.Empty, rawValue);
        }

        [Theory]
        [ClassData(typeof(IsDefaultTestData))]
        public void IsDefaultTest(ConcurrencyToken concurrencyToken, bool expectedIsDefault)
        {
            // Arrange
            // -

            // Act
            var isDefault = concurrencyToken.IsDefault;

            // Assert
            Assert.Equal(expectedIsDefault, isDefault);
        }

        public class IsDefaultTestData : TheoryData<ConcurrencyToken, bool>
        {
            public IsDefaultTestData()
            {
                Add(default, true);
                Add(new ConcurrencyToken(), true);
                Add(new ConcurrencyToken("sokvm"), false);
                Add(new ConcurrencyToken("  "), false);
            }
        }

        [Theory]
        [ClassData(typeof(EqualityTestData))]
        public void EqualityOperationTest(ConcurrencyToken left, ConcurrencyToken right, bool expectedAreEqual)
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
        public void InequalityOperationTest(ConcurrencyToken left, ConcurrencyToken right, bool expectedAreEqual)
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
        public void EqualsOperationTest(ConcurrencyToken left, ConcurrencyToken right, bool expectedAreEqual)
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
        public void ObjectEqualsOperationTest(ConcurrencyToken left, ConcurrencyToken right, bool expectedAreEqual)
        {
            // Arrange
            var other = (object)right;

            // Act
            var areEqual = left.Equals(other);

            // Assert
            Assert.Equal(expectedAreEqual, areEqual);
        }

        public class EqualityTestData : TheoryData<ConcurrencyToken, ConcurrencyToken, bool>
        {
            public EqualityTestData()
            {
                Add(default, default, true);
                Add(default, _concurrencyToken0, true);
                Add(default, _concurrencyToken1, false);
                Add(default, _concurrencyToken2, false);

                Add(_concurrencyToken0, default, true);
                Add(_concurrencyToken0, _concurrencyToken0, true);
                Add(_concurrencyToken0, _concurrencyToken1, false);
                Add(_concurrencyToken0, _concurrencyToken2, false);

                Add(_concurrencyToken1, default, false);
                Add(_concurrencyToken1, _concurrencyToken0, false);
                Add(_concurrencyToken1, _concurrencyToken1, true);
                Add(_concurrencyToken1, _concurrencyToken2, false);

                Add(_concurrencyToken2, default, false);
                Add(_concurrencyToken2, _concurrencyToken0, false);
                Add(_concurrencyToken2, _concurrencyToken1, false);
                Add(_concurrencyToken2, _concurrencyToken2, true);

                Add(default, ConcurrencyToken.NoConcurrencyToken, true);
            }
        }

        [Theory]
        [ClassData(typeof(SubsequentHashCodeCallsReturnSameHashCodeTestData))]
        public void SubsequentHashCodeCallsReturnSameHashCodeTest(ConcurrencyToken domainEvent)
        {
            // Arrange
            var expectedHashCode = domainEvent.GetHashCode();

            // Act
            var hashCode = domainEvent.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class SubsequentHashCodeCallsReturnSameHashCodeTestData : TheoryData<ConcurrencyToken>
        {
            public SubsequentHashCodeCallsReturnSameHashCodeTestData()
            {
                Add(default);
                Add(_concurrencyToken0);
                Add(_concurrencyToken1);
                Add(_concurrencyToken2);
            }
        }

        [Theory]
        [ClassData(typeof(EqualValuesReturnsSameHashCodeTestData))]
        public void EqualValuesReturnsSameHashCodeTest(ConcurrencyToken left, ConcurrencyToken right)
        {
            // Arrange
            var expectedHashCode = left.GetHashCode();

            // Act
            var hashCode = right.GetHashCode();

            // Assert
            Assert.Equal(expectedHashCode, hashCode);
        }

        public class EqualValuesReturnsSameHashCodeTestData : TheoryData<ConcurrencyToken, ConcurrencyToken>
        {
            public EqualValuesReturnsSameHashCodeTestData()
            {
                Add(default, default);
                Add(_concurrencyToken0, default);
                Add(default, _concurrencyToken0);

                Add(_concurrencyToken1, _concurrencyToken1);
                Add(_concurrencyToken2, _concurrencyToken2);
            }
        }

        [Theory]
        [ClassData(typeof(FromStringTestData))]
        public void FromStringTest(string? rawValue, ConcurrencyToken expectedConcurrencyToken)
        {
            // Arrange
            // -

            // Act
            var concurrencyToken = ConcurrencyToken.FromString(rawValue);

            // Assert
            Assert.Equal(expectedConcurrencyToken, concurrencyToken);
        }

        [Theory]
        [ClassData(typeof(FromStringTestData))]
        public void ImplicitOperatorFromStringTest(string? rawValue, ConcurrencyToken expectedConcurrencyToken)
        {
            // Arrange
            // -

            // Act
            var concurrencyToken = (ConcurrencyToken)rawValue;

            // Assert
            Assert.Equal(expectedConcurrencyToken, concurrencyToken);
        }

        public class FromStringTestData : TheoryData<string?, ConcurrencyToken>
        {
            public FromStringTestData()
            {
                Add(null, default);
                Add(string.Empty, default);
                Add("sokvm", _concurrencyToken1);
                Add("  ", _concurrencyToken2);
            }
        }
    }
}
