using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination
{
    [TestClass]
    public class CoordinationEntryPathSegmentTests
    {
        [TestMethod]
        public void DefaultTest()
        {
            var coordinationEntryPathSegment = default(CoordinationEntryPathSegment);

            Assert.IsTrue(coordinationEntryPathSegment.Segment.IsEmpty);
            Assert.IsTrue(coordinationEntryPathSegment.EscapedSegment.IsEmpty);
            Assert.AreEqual(string.Empty, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void EmptyUnescapedEqualsDefaultTest()
        {
            var unescaped = string.Empty;

            var coordinationEntryPathSegment = new CoordinationEntryPathSegment(unescaped);
            Assert.IsTrue(coordinationEntryPathSegment.Segment.IsEmpty);
            Assert.IsTrue(coordinationEntryPathSegment.EscapedSegment.IsEmpty);
            Assert.AreEqual(string.Empty, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void EmptyEscapedEqualsDefaultTest()
        {
            var escaped = string.Empty;

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            Assert.IsTrue(coordinationEntryPathSegment.Segment.IsEmpty);
            Assert.IsTrue(coordinationEntryPathSegment.EscapedSegment.IsEmpty);
            Assert.AreEqual(string.Empty, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void EscapeNonNeededTest()
        {
            var unescaped = @"abcd";
            var escaped = unescaped;

            var coordinationEntryPathSegment = new CoordinationEntryPathSegment(unescaped);

            Assert.IsTrue(unescaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.Segment.Span));
            Assert.IsTrue(escaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.EscapedSegment.Span));
            Assert.AreEqual(unescaped, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void UnescapeNonNeededTest()
        {
            var unescaped = @"abcd";
            var escaped = unescaped;

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(unescaped.AsMemory());

            Assert.IsTrue(unescaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.Segment.Span));
            Assert.IsTrue(escaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.EscapedSegment.Span));
            Assert.AreEqual(unescaped, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void EscapeTest()
        {
            var unescaped = @"/abc/d-ef///\xyz\\\oz";
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Yoz";

            var coordinationEntryPathSegment = new CoordinationEntryPathSegment(unescaped);

            Assert.IsTrue(unescaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.Segment.Span));
            Assert.IsTrue(escaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.EscapedSegment.Span));
            Assert.AreEqual(unescaped, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void UnescapeTest()
        {
            var unescaped = @"/abc/d-ef///\xyz\\\oz";
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Yoz";

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());

            Assert.IsTrue(unescaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.Segment.Span));
            Assert.IsTrue(escaped.AsSpan().SequenceEqual(coordinationEntryPathSegment.EscapedSegment.Span));
            Assert.AreEqual(unescaped, coordinationEntryPathSegment.ToString());
        }

        [TestMethod]
        public void EqualsSelfTest()
        {
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Y";

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());

            Assert.IsTrue(coordinationEntryPathSegment.Equals(coordinationEntryPathSegment));
            Assert.IsTrue(coordinationEntryPathSegment.Equals((object)coordinationEntryPathSegment));
#pragma warning disable CS1718
            Assert.IsTrue(coordinationEntryPathSegment == coordinationEntryPathSegment);
            Assert.IsFalse(coordinationEntryPathSegment != coordinationEntryPathSegment);
#pragma warning restore CS1718          
        }

        [TestMethod]
        public void UnequalsNullTest()
        {
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Y";

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            Assert.IsFalse(coordinationEntryPathSegment.Equals(null));
        }

        [TestMethod]
        public void EqualsFromEqualUnescapedValueTest()
        {
            var unescaped = @"/abc/d-ef///\xyz\\\";
            var unescaped2 = unescaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = new CoordinationEntryPathSegment(unescaped);
            var coordinationEntryPathSegment2 = new CoordinationEntryPathSegment(unescaped2);

            Assert.IsTrue(coordinationEntryPathSegment.Equals(coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment.Equals((object)coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment == coordinationEntryPathSegment2);
            Assert.IsFalse(coordinationEntryPathSegment != coordinationEntryPathSegment2);
        }

        [TestMethod]
        public void EqualsFromEqualEscapedValueTest()
        {
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Y";
            var escaped2 = escaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            var coordinationEntryPathSegment2 = CoordinationEntryPathSegment.FromEscapedSegment(escaped2.AsMemory());

            Assert.IsTrue(coordinationEntryPathSegment.Equals(coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment.Equals((object)coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment == coordinationEntryPathSegment2);
            Assert.IsFalse(coordinationEntryPathSegment != coordinationEntryPathSegment2);
        }

        [TestMethod]
        public void HashCodeForEqualUnescapedValuesTest()
        {
            var unescaped = @"/abc/d-ef///\xyz\\\";
            var unescaped2 = unescaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = new CoordinationEntryPathSegment(unescaped);
            var coordinationEntryPathSegment2 = new CoordinationEntryPathSegment(unescaped2);

            Assert.IsTrue(coordinationEntryPathSegment.GetHashCode() == coordinationEntryPathSegment2.GetHashCode());
        }

        [TestMethod]
        public void HashCodeForEqualEscapedValuesTest()
        {
            var escaped = @"-Xabc-Xd--ef-X-X-X-Yxyz-Y-Y-Y";
            var escaped2 = escaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            var coordinationEntryPathSegment2 = CoordinationEntryPathSegment.FromEscapedSegment(escaped2.AsMemory());

            Assert.IsTrue(coordinationEntryPathSegment.GetHashCode() == coordinationEntryPathSegment2.GetHashCode());
        }

        [TestMethod]
        public void EscapedContainsPathDelimiterTest()
        {
            var escaped = @"abc/def";

            Assert.ThrowsException<ArgumentException>(() =>
            {
                CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            });
        }

        [TestMethod]
        public void EscapedContainsAltPathDelimiterTest()
        {
            var escaped = @"abc\def";

            Assert.ThrowsException<ArgumentException>(() =>
            {
                CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            });
        }

        [TestMethod]
        public void EscapedContainsIllegalCharAfterEscapeCharTest()
        {
            var escaped = @"abc-Gdef";

            Assert.ThrowsException<ArgumentException>(() =>
            {
                CoordinationEntryPathSegment.FromEscapedSegment(escaped.AsMemory());
            });
        }
    }
}
