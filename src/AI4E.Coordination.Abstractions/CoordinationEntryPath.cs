using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using AI4E.Utils;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public readonly struct CoordinationEntryPath : IEquatable<CoordinationEntryPath>
    {
        public const char PathDelimiter = '/';
        public const char AltPathDelimiter = '\\';
        private const string _pathDelimiterString = "/";
        private static readonly ImmutableList<CoordinationEntryPath> _rootAncestors = ImmutableList.Create(default(CoordinationEntryPath));

        private readonly ImmutableList<CoordinationEntryPathSegment> _segments;

        public CoordinationEntryPath(CoordinationEntryPathSegment segment)
        {
            if (segment == default)
                throw new ArgumentDefaultException(nameof(segment));

            _segments = ImmutableList<CoordinationEntryPathSegment>.Empty.Add(segment);
        }

        public CoordinationEntryPath(params string[] segments) : this((IEnumerable<string>)segments) { }

        public CoordinationEntryPath(IEnumerable<string> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            var segmentsBuilder = ImmutableList.CreateBuilder<CoordinationEntryPathSegment>();

            foreach (var segment in segments)
            {
                if (segment == null)
                    throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

                CoordinationEntryPathSegment segmentX;

                try
                {
                    segmentX = new CoordinationEntryPathSegment(segment.AsMemory());
                }
                catch (ArgumentException exc)
                {
                    throw new ArgumentException("The collection must not contain emtpy entries or entries that contain whitespace only.", nameof(segments), exc);
                }

                segmentsBuilder.Add(segmentX);
            }

            _segments = segmentsBuilder.ToImmutable();
        }

        public CoordinationEntryPath(params CoordinationEntryPathSegment[] segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

            _segments = segments.ToImmutableList();
        }

        public CoordinationEntryPath(IEnumerable<CoordinationEntryPathSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

            _segments = segments.ToImmutableList();
        }

        public IReadOnlyList<CoordinationEntryPathSegment> Segments => _segments ?? ImmutableList<CoordinationEntryPathSegment>.Empty;

        public bool IsRoot => Segments.Count == 0;

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
                resultsWriter.Append(PathDelimiter);

                for (var i = 0; i < _segments.Count; i++)
                {
                    resultsWriter.Append(_segments[i].EscapedSegment.Span);
                    resultsWriter.Append(PathDelimiter);
                }

                var memory = resultsWriter.GetMemory();
                Assert(memory.Length == result.Length);
                return memory;
            }
        }

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

        public override bool Equals(object obj)
        {
            return obj is CoordinationEntryPath path && Equals(path);
        }

        public override int GetHashCode()
        {
            return Segments.GetSequenceHashCode();
        }

        public override string ToString()
        {
            return EscapedPath.ConvertToString();
        }

        public static bool operator ==(CoordinationEntryPath left, CoordinationEntryPath right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CoordinationEntryPath left, CoordinationEntryPath right)
        {
            return !left.Equals(right);
        }

        public static CoordinationEntryPath FromEscapedPath(ReadOnlyMemory<char> path)
        {
            var span = path.Span;
            var segmentStart = 0;
            var segments = new List<CoordinationEntryPathSegment>();

            void ProcessSegment(int exclusiveEnd)
            {
                var slice = path.Slice(segmentStart, exclusiveEnd: exclusiveEnd);

                if (!slice.Span.IsEmptyOrWhiteSpace())
                {
                    var segment = CoordinationEntryPathSegment.FromEscapedSegment(slice);
                    Assert(segment != default);
                    segments.Add(segment);
                }
            }

            for (var i = 0; i < path.Length; i++)
            {
                switch (span[i])
                {
                    case PathDelimiter:
                    case AltPathDelimiter:

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
        public IReadOnlyCollection<CoordinationEntryPath> GetAncestorPaths()
        {
            // If we are the root node path, we return a collection of size one that contains the root node path.
            // This is to be in sync with GetParentPath() that returns the root node path as the root nodes parent path.

            if(_segments == null || !_segments.Any() || _segments.Count == 1)
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

        public CoordinationEntryPath GetChildPath(CoordinationEntryPathSegment segment)
        {
            if (_segments == null || !_segments.Any())
                return new CoordinationEntryPath(segment);

            return new CoordinationEntryPath(_segments.Add(segment));
        }

        public CoordinationEntryPath GetChildPath(params CoordinationEntryPathSegment[] segments)
        {
            return GetChildPath((IEnumerable<CoordinationEntryPathSegment>)segments);
        }

        public CoordinationEntryPath GetChildPath(IEnumerable<CoordinationEntryPathSegment> segments)
        {
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
