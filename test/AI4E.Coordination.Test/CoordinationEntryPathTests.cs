/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AI4E.Coordination
{
    [TestClass]
    public class CoordinationEntryPathTests
    {
        [TestMethod]
        public void DefaultTest()
        {
            var coordinationEntryPath = default(CoordinationEntryPath);

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void SingleDefaultSegmentEqualsDefaultTest()
        {
            var coordinationEntryPath = new CoordinationEntryPath(new CoordinationEntryPathSegment());

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void FromEmptySegmentsEqualsDefaultTest()
        {
            var coordinationEntryPath = new CoordinationEntryPath(new CoordinationEntryPathSegment[0]);

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void FromEmptyStringsEqualsDefaultTest()
        {
            var coordinationEntryPath = new CoordinationEntryPath(new string[0]);

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void FromDefaultSegmentsEqualsDefaultTest()
        {
            var coordinationEntryPath = new CoordinationEntryPath(
                new CoordinationEntryPathSegment[]{
                    default, default
                });

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void FromSegmentTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
            };

            var escapedPath = "/ab-Xc/";

            var path = new CoordinationEntryPath(segments[0]);

            Assert.IsTrue(segments.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void FromSegmentsTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var path = new CoordinationEntryPath(segments);

            Assert.IsTrue(segments.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void FromStringsTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var path = new CoordinationEntryPath(segments.Select(p => p.Segment.ToString()));

            Assert.IsTrue(segments.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void FromSegmentsWithDefaultTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment(),
                new CoordinationEntryPathSegment("ghi")
            };

            var withoutDefault = new[]
            {
                segments[0], segments[1], segments[3]
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var path = new CoordinationEntryPath(segments);

            Assert.IsTrue(withoutDefault.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void FromStringsWithDefaultTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment(),
                new CoordinationEntryPathSegment("ghi")
            };

            var withoutDefault = new[]
            {
                segments[0], segments[1], segments[3]
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var path = new CoordinationEntryPath(segments.Select(p => p.Segment.ToString()));

            Assert.IsTrue(withoutDefault.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void RootGetParentPathTest()
        {
            var coordinationEntryPath = default(CoordinationEntryPath).GetParentPath();

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void RootChildGetParentPathTest()
        {
            var coordinationEntryPath = new CoordinationEntryPath("abc").GetParentPath();

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void GetParentPathTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var parentSegments = new[]
            {
                segments[0],
                segments[1]
            };

            var escapedPath = "/ab-Xc/d-Yef/";

            var path = new CoordinationEntryPath(segments).GetParentPath();

            Assert.IsTrue(parentSegments.SequenceEqual(path.Segments));
            Assert.IsFalse(path.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(path.EscapedPath.Span));
        }

        [TestMethod]
        public void RootGetAncestorPathsTest()
        {
            var coordinationEntryPaths = default(CoordinationEntryPath).GetAncestorPaths();
            var coordinationEntryPath = coordinationEntryPaths.First();

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void RootChildGetAncestorPathsTest()
        {
            var coordinationEntryPaths = new CoordinationEntryPath("abc").GetAncestorPaths();
            var coordinationEntryPath = coordinationEntryPaths.First();

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void GetAncestorPathsTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var parentSegments = new[] { segments[0], segments[1] };
            var grandParentSegments = new[] { segments[0] };

            var parentEscapedPath = "/ab-Xc/d-Yef/";
            var grandParentEscapedPath = "/ab-Xc/";

            var coordinationEntryPaths = new CoordinationEntryPath(segments).GetAncestorPaths();
            var root = coordinationEntryPaths.First();
            var grandParent = coordinationEntryPaths.Skip(1).First();
            var parent = coordinationEntryPaths.Last();

            Assert.AreEqual(0, root.Segments.Count);
            Assert.IsTrue(root.IsRoot);
            Assert.AreEqual("/", root.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(root.EscapedPath.Span));

            Assert.IsTrue(grandParentSegments.SequenceEqual(grandParent.Segments));
            Assert.IsFalse(grandParent.IsRoot);
            Assert.IsTrue(grandParentEscapedPath.AsSpan().SequenceEqual(grandParent.EscapedPath.Span));

            Assert.IsTrue(parentSegments.SequenceEqual(parent.Segments));
            Assert.IsFalse(parent.IsRoot);
            Assert.IsTrue(parentEscapedPath.AsSpan().SequenceEqual(parent.EscapedPath.Span));
        }

        [TestMethod]
        public void GetDirectChildPathTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var parentSegments = new[]
            {
                segments[0],
                segments[1]
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var child = new CoordinationEntryPath(parentSegments).GetChildPath(segments[2]);

            Assert.IsTrue(segments.SequenceEqual(child.Segments));
            Assert.IsFalse(child.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(child.EscapedPath.Span));
        }

        [TestMethod]
        public void GetChildPathTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var parentSegments = new[]
            {
                segments[0]
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var child = new CoordinationEntryPath(parentSegments).GetChildPath(new[]
            {
                segments[1],
                segments[2]
            });

            Assert.IsTrue(segments.SequenceEqual(child.Segments));
            Assert.IsFalse(child.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(child.EscapedPath.Span));
        }

        [TestMethod]
        public void GetChildPathWithDefaultSegmentTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var child = new CoordinationEntryPath(segments).GetChildPath(
                new CoordinationEntryPathSegment());

            Assert.IsTrue(segments.SequenceEqual(child.Segments));
            Assert.IsFalse(child.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(child.EscapedPath.Span));
        }

        [TestMethod]
        public void GetChildPathWithEmptySegmentsTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var child = new CoordinationEntryPath(segments).GetChildPath(
                new CoordinationEntryPathSegment[0]);

            Assert.IsTrue(segments.SequenceEqual(child.Segments));
            Assert.IsFalse(child.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(child.EscapedPath.Span));
        }

        [TestMethod]
        public void FromEscapedPathTest()
        {
            var segments = new[]
            {
                new CoordinationEntryPathSegment("ab/c"),
                new CoordinationEntryPathSegment("d\\ef"),
                new CoordinationEntryPathSegment("ghi")
            };

            var escapedPath = "/ab-Xc/d-Yef/ghi/";

            var child = CoordinationEntryPath.FromEscapedPath(escapedPath.AsMemory());

            Assert.IsTrue(segments.SequenceEqual(child.Segments));
            Assert.IsFalse(child.IsRoot);
            Assert.IsTrue(escapedPath.AsSpan().SequenceEqual(child.EscapedPath.Span));
        }

        [TestMethod]
        public void FromEmptyEscapedPathTest()
        {
            var coordinationEntryPath = CoordinationEntryPath.FromEscapedPath("".AsMemory());

            Assert.AreEqual(0, coordinationEntryPath.Segments.Count);
            Assert.IsTrue(coordinationEntryPath.IsRoot);
            Assert.AreEqual("/", coordinationEntryPath.ToString());
            Assert.IsTrue("/".AsSpan().SequenceEqual(coordinationEntryPath.EscapedPath.Span));
        }

        [TestMethod]
        public void EqualsSelfTest()
        {
            var coordinationEntryPath = CoordinationEntryPath.FromEscapedPath(
                "/ab-Xc/d-Yef/ghi/".AsMemory());

            Assert.IsTrue(coordinationEntryPath.Equals(coordinationEntryPath));
            Assert.IsTrue(coordinationEntryPath.Equals((object)coordinationEntryPath));
#pragma warning disable CS1718
            Assert.IsTrue(coordinationEntryPath == coordinationEntryPath);
            Assert.IsFalse(coordinationEntryPath != coordinationEntryPath);
#pragma warning restore CS1718     
        }

        [TestMethod]
        public void UnequalsNullTest()
        {
            var coordinationEntryPath = CoordinationEntryPath.FromEscapedPath(
               "/ab-Xc/d-Yef/ghi/".AsMemory());
            Assert.IsFalse(coordinationEntryPath.Equals(null));
        }

        [TestMethod]
        public void EqualsFromEqualEscapedPathTest()
        {
            var escaped = @"/ab-Xc/d-Yef/ghi/";
            var escaped2 = escaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = CoordinationEntryPath.FromEscapedPath(
               escaped.AsMemory());
            var coordinationEntryPathSegment2 = CoordinationEntryPath.FromEscapedPath(
               escaped2.AsMemory());

            Assert.IsTrue(coordinationEntryPathSegment.Equals(coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment.Equals((object)coordinationEntryPathSegment2));
            Assert.IsTrue(coordinationEntryPathSegment == coordinationEntryPathSegment2);
            Assert.IsFalse(coordinationEntryPathSegment != coordinationEntryPathSegment2);
        }

        [TestMethod]
        public void HashCodeForEqualEscapedPathsTest()
        {
            var escaped = @"/ab-Xc/d-Yef/ghi/";
            var escaped2 = escaped.AsSpan().ToArray();

            var coordinationEntryPathSegment = CoordinationEntryPath.FromEscapedPath(
               escaped.AsMemory());
            var coordinationEntryPathSegment2 = CoordinationEntryPath.FromEscapedPath(
               escaped2.AsMemory());

            Assert.IsTrue(coordinationEntryPathSegment.GetHashCode() == coordinationEntryPathSegment2.GetHashCode());
        }
    }
}
