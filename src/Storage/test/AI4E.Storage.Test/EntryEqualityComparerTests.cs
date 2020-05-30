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
using AI4E.Storage.Test.TestTypes;
using Xunit;

namespace AI4E.Storage.Test
{
    public sealed class EntryEqualityComparerTests
    {
        [Fact]
        public void GetHashCodeNullEntryThrowsArgumentNullExceptionTest()
        {
            Assert.Throws<ArgumentNullException>("obj", () =>
            {
                EntryEqualityComparer<object>.Instance.GetHashCode(null);
            });
        }

        [Fact]
        public void GetHashCodeNoIdEntryTest()
        {
            var entry = new NoIdEntry();
            var hashCode = EntryEqualityComparer<NoIdEntry>.Instance.GetHashCode(entry);

            Assert.Equal(entry.GetHashCode(), hashCode);
        }

        [Fact]
        public void GetHashCodeIdEntryTest()
        {
            var entry = new IdEntry() { Id = 22 };
            var hashCode = EntryEqualityComparer<IdEntry>.Instance.GetHashCode(entry);

            Assert.Equal(entry.Id.GetHashCode(), hashCode);
        }

        [Fact]
        public void GetHashCodeIdEntryWithNoIdEntryBaseTest()
        {
            var entry = new IdEntryWithNoIdEntryBase() { Id = 22 };
            var hashCode = EntryEqualityComparer<NoIdEntry>.Instance.GetHashCode(entry);

            Assert.Equal(entry.GetHashCode(), hashCode);
        }

        [Fact]
        public void EqualsNullArgumentsTest()
        {
            var equals = EntryEqualityComparer<IdEntry>.Instance.Equals(null, null);

            Assert.True(equals);
        }

        [Fact]
        public void EqualsNullXArgumentTest()
        {
            var entry = new IdEntry { Id = 22 };
            var equals = EntryEqualityComparer<IdEntry>.Instance.Equals(null, entry);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsNullYArgumentTest()
        {
            var entry = new IdEntry { Id = 22 };
            var equals = EntryEqualityComparer<IdEntry>.Instance.Equals(entry, null);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsReferenceEqualsArgumentsTest()
        {
            var entry = new NoIdEntry();
            var equals = EntryEqualityComparer<NoIdEntry>.Instance.Equals(entry, entry);

            Assert.True(equals);
        }

        [Fact]
        public void EqualsXArgumentNullIdNullableValueTypeTest()
        {
            var x = new IdEntry<int?>();
            var y = new IdEntry<int?>() { Id = 5 };

            var equals = EntryEqualityComparer<IdEntry<int?>>.Instance.Equals(x, y);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsYArgumentNullIdNullableValueTypeTest()
        {
            var x = new IdEntry<int?>() { Id = 5 };
            var y = new IdEntry<int?>();

            var equals = EntryEqualityComparer<IdEntry<int?>>.Instance.Equals(x, y);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsXArgumentNullIdReferenceTypeTest()
        {
            var x = new IdEntry<string>();
            var y = new IdEntry<string>() { Id = "a" };

            var equals = EntryEqualityComparer<IdEntry<string>>.Instance.Equals(x, y);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsYArgumentNullIdReferenceTypeTest()
        {
            var x = new IdEntry<string>() { Id = "a" };
            var y = new IdEntry<string>();

            var equals = EntryEqualityComparer<IdEntry<string>>.Instance.Equals(x, y);

            Assert.False(equals);
        }

        [Fact]
        public void EqualsEqualIdTest()
        {
            var x = new IdEntry() { Id = 22 };
            var y = new IdEntry() { Id = 22 };
            var equals = EntryEqualityComparer<IdEntry>.Instance.Equals(x, y);

            Assert.True(equals);
        }
    }
}
