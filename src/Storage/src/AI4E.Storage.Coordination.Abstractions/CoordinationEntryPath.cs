/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Diagnostics;
using System.Linq;
using AI4E.Utils.Memory;

namespace AI4E.Storage.Coordination
{
#if NETCORE30
    using static System.MemoryExtensions;
#endif

    /// <summary>
    /// Represents a coordination entry path.
    /// </summary>
    public readonly struct CoordinationEntryPath : IEquatable<CoordinationEntryPath>
    {
        internal const char _pathDelimiter = '/';
        internal const char _altPathDelimiter = '\\';
        private static readonly string _pathDelimiterString = _pathDelimiter.ToString();
        private static readonly ImmutableList<CoordinationEntryPath> _rootAncestors = ImmutableList.Create(default(CoordinationEntryPath));

        private readonly ImmutableList<CoordinationEntryPathSegment> _segments;

        /// <summary>
        /// Creates a new <see cref="CoordinationEntryPath"/> from the specified <see cref="CoordinationEntryPathSegment"/>.
        /// </summary>
        /// <param name="segment">The <see cref="CoordinationEntryPathSegment"/> that is the single segment of the path.</param>
        public CoordinationEntryPath(CoordinationEntryPathSegment segment)
        {
            if (segment == default)
            {
                this = default;
                return;
            }

            _segments = ImmutableList<CoordinationEntryPathSegment>.Empty.Add(segment);
        }

        /// <summary>
        /// Creates a new <see cref="CoordinationEntryPath"/> from the specified collection of unescaped string segments.
        /// </summary>
        /// <param name="segments">A collection of strings that represent the unescaped segments of the path.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="segments"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if any of the strings contained in <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath(params string[] segments) : this(segments as IEnumerable<string>) { }

        /// <summary>
        /// Creates a new <see cref="CoordinationEntryPath"/> from the specified collection of unescaped string segments.
        /// </summary>
        /// <param name="segments">A collection of strings that represent the unescaped segments of the path.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="segments"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">Thrown if any of the strings contained in <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath(IEnumerable<string> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            var segmentsBuilder = ImmutableList.CreateBuilder<CoordinationEntryPathSegment>();

            foreach (var escapedSegment in segments)
            {
                if (escapedSegment == null)
                    throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

                var segment = new CoordinationEntryPathSegment(escapedSegment.AsMemory());

                if (segment != default)
                {
                    segmentsBuilder.Add(segment);
                }
            }

            _segments = segmentsBuilder.ToImmutable();
        }

        /// <summary>
        /// Creates a new <see cref="CoordinationEntryPath"/> from the specified collection of <see cref="CoordinationEntryPathSegment"/>s.
        /// </summary>
        /// <param name="segments">The paths segments.</param>
        /// <exception cref="ArgumentNullException">Thrown is <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath(params CoordinationEntryPathSegment[] segments) : this(segments as IEnumerable<CoordinationEntryPathSegment>) { }

        /// <summary>
        /// Creates a new <see cref="CoordinationEntryPath"/> from the specified collection of <see cref="CoordinationEntryPathSegment"/>s.
        /// </summary>
        /// <param name="segments">The paths segments.</param>
        /// <exception cref="ArgumentNullException">Thrown is <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath(IEnumerable<CoordinationEntryPathSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (!segments.Any(p => p == default))
            {
                _segments = segments as ImmutableList<CoordinationEntryPathSegment> ?? segments.ToImmutableList();
            }
            else
            {
                _segments = segments.Where(p => p != default).ToImmutableList();
            }
        }

        /// <summary>
        /// Gets the collection of segments, the <see cref="CoordinationEntryPath"/> is composed of.
        /// </summary>
        public IReadOnlyList<CoordinationEntryPathSegment> Segments => _segments ?? ImmutableList<CoordinationEntryPathSegment>.Empty;

        /// <summary>
        /// Gets a boolean value indicating whether the <see cref="CoordinationEntryPath"/> specifies the root node.
        /// </summary>
        public bool IsRoot => Segments.Count == 0;

        /// <summary>
        /// Gets the memory of chars that represent the escaped <see cref="CoordinationEntryPath"/>.
        /// </summary>
        public ReadOnlyMemory<char> EscapedPath
        {
            get
            {
                if (_segments == null || !_segments.Any())
                {
                    return _pathDelimiterString.AsMemory();
                }

                var capacity = 1;

                for (var i = 0; i < _segments.Count; i++)
                {
                    capacity += _segments[i].EscapedSegment.Length + 1;
                }

                var result = new char[capacity];
                var resultsWriter = new MemoryWriter<char>(result);
                resultsWriter.Append(_pathDelimiter);

                for (var i = 0; i < _segments.Count; i++)
                {
                    resultsWriter.Append(_segments[i].EscapedSegment.Span);
                    resultsWriter.Append(_pathDelimiter);
                }

                var memory = resultsWriter.GetMemory();
                Debug.Assert(memory.Length == result.Length);
                return memory;
            }
        }

        /// <inheritdoc/>
        public bool Equals(CoordinationEntryPath other)
        {
            if (Segments.Count != other.Segments.Count)
                return false;

            for (var i = 0; i < Segments.Count; i++)
            {
                if (Segments[i] != other.Segments[i])
                    return false;
            }

            return true;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CoordinationEntryPath path && Equals(path);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Segments.SequenceHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return EscapedPath.ConvertToString();
        }

        /// <summary>
        /// Compares two <see cref="CoordinationEntryPath"/>s.
        /// </summary>
        /// <param name="left">The first path.</param>
        /// <param name="right">The second path.</param>
        /// <returns>True, if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(CoordinationEntryPath left, CoordinationEntryPath right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CoordinationEntryPath"/>s.
        /// </summary>
        /// <param name="left">The first path.</param>
        /// <param name="right">The second path.</param>
        /// <returns>True, if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(CoordinationEntryPath left, CoordinationEntryPath right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a <see cref="CoordinationEntryPath"/> from the specified escaped path.
        /// </summary>
        /// <param name="path">A memory of chars that represent the escaped <see cref="CoordinationEntryPath"/>.</param>
        /// <returns>The <see cref="CoordinationEntryPath"/> created from <paramref name="path"/>.</returns>
        public static CoordinationEntryPath FromEscapedPath(ReadOnlyMemory<char> path)
        {
            var span = path.Span;
            var segmentStart = 0;
            var segments = new List<CoordinationEntryPathSegment>();

            void ProcessSegment(int exclusiveEnd)
            {
                var slice = path.Slice(segmentStart, exclusiveEnd - segmentStart);

                if (!slice.Span.IsEmptyOrWhiteSpace())
                {
                    var segment = CoordinationEntryPathSegment.FromEscapedSegment(slice);
                    Debug.Assert(segment != default);
                    segments.Add(segment);
                }
            }

            for (var i = 0; i < path.Length; i++)
            {
                switch (span[i])
                {
                    case _pathDelimiter:
                    case _altPathDelimiter:

                        ProcessSegment(i);
                        segmentStart = i + 1;
                        break;
                }
            }

            ProcessSegment(path.Length);

            if (!segments.Any())
                return default;

            return new CoordinationEntryPath(segments);
        }

        /// <summary>
        /// Returns the path's parent.
        /// </summary>
        /// <returns>The <see cref="CoordinationEntryPath"/> that specifies the path's parent path.</returns>
        /// <remarks>
        /// If the current path is the root path, the result will be the root path too.
        /// This is equivalent to a unix filesystem, where .. of the filesystem root equals the filesystem root itself.
        /// </remarks>
        public CoordinationEntryPath GetParentPath()
        {
            if (_segments == null || !_segments.Any())
            {
                return this;
            }

            if (_segments.Count == 1)
            {
                return default;
            }

            return new CoordinationEntryPath(_segments.RemoveAt(_segments.Count - 1));
        }

        /// <summary>
        /// Returns the ancestors paths from root path to parent path.
        /// </summary>
        /// <returns>The paths of the ancestor nodes.</returns>
        /// <remarks>
        /// The result will contain the root path as first argument, regardless of the input.
        /// If we are the root node path, we return a collection of size one that contains the root node path.
        /// This is to be consistent with <see cref="GetParentPath"/> that returns the root node path as the root nodes parent path.
        /// </remarks>
        public IReadOnlyCollection<CoordinationEntryPath> GetAncestorPaths()
        {
            if (_segments == null || !_segments.Any() || _segments.Count == 1)
            {
                return _rootAncestors;
            }

            var result = new Stack<CoordinationEntryPath>();

            for (var current = GetParentPath(); current != default; current = current.GetParentPath())
            {
                result.Push(current);
            }

            result.Push(default);

            return result;
        }

        /// <summary>
        /// Returns the child path of the current path with the specified relative path.
        /// </summary>
        /// <param name="segment">A <see cref="CoordinationEntryPathSegment"/> that specifies the relative path.</param>
        /// <returns>The resulting <see cref="CoordinationEntryPath"/>.</returns>
        public CoordinationEntryPath GetChildPath(CoordinationEntryPathSegment segment)
        {
            if (segment == default)
                return this;

            if (_segments == null || !_segments.Any())
                return new CoordinationEntryPath(segment);

            return new CoordinationEntryPath(_segments.Add(segment));
        }

        /// <summary>
        /// Returns the child path of the current path with the specified relative path.
        /// </summary>
        /// <param name="segments">The <see cref="CoordinationEntryPathSegment"/>s that specify the relative path.</param>
        /// <returns>The resulting <see cref="CoordinationEntryPath"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath GetChildPath(params CoordinationEntryPathSegment[] segments)
        {
            return GetChildPath((IEnumerable<CoordinationEntryPathSegment>)segments);
        }

        /// <summary>
        /// Returns the child path of the current path with the specified relative path.
        /// </summary>
        /// <param name="segments">The <see cref="CoordinationEntryPathSegment"/>s that specify the relative path.</param>
        /// <returns>The resulting <see cref="CoordinationEntryPath"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="segments"/> is <c>null</c>.</exception>
        public CoordinationEntryPath GetChildPath(IEnumerable<CoordinationEntryPathSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            return new CoordinationEntryPath((_segments ?? ImmutableList<CoordinationEntryPathSegment>.Empty).AddRange(segments));
        }
    }

    public static class CoordinationEntryPathExtension
    {
        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, IEnumerable<string> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            return path.GetChildPath(segments.Select(p => new CoordinationEntryPathSegment(p)));
        }

        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, params string[] segments)
        {
            return path.GetChildPath(segments as IEnumerable<string>);
        }

        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, string segment)
        {
            return path.GetChildPath(new CoordinationEntryPathSegment(segment));
        }

        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, IEnumerable<ReadOnlyMemory<char>> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            return path.GetChildPath(segments.Select(p => new CoordinationEntryPathSegment(p)));
        }

        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, params ReadOnlyMemory<char>[] segments)
        {
            return path.GetChildPath(segments as IEnumerable<ReadOnlyMemory<char>>);
        }

        public static CoordinationEntryPath GetChildPath(this CoordinationEntryPath path, ReadOnlyMemory<char> segment)
        {
            return path.GetChildPath(new CoordinationEntryPathSegment(segment));
        }
    }
}
