using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.InteropServices;
using AI4E.Utils.Memory;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
#if NETCORE30
    using static System.MemoryExtensions;
#endif

    public readonly struct CoordinationEntryPath : IEquatable<CoordinationEntryPath>
    {
        public const char PathDelimiter = '/';
        public const char AltPathDelimiter = '\\';
        private const string _pathDelimiterString = "/";

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
            return Segments.SequenceHashCode();
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
                var slice = path.Slice(segmentStart, exclusiveEnd - segmentStart);

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

    public readonly struct CoordinationEntryPathSegment : IEquatable<CoordinationEntryPathSegment>
    {
        public const char EscapeChar = '-';
        public const char PathDelimiterReplacement = 'X';
        public const char AltPathDelimiterReplacement = 'Y';

        public CoordinationEntryPathSegment(string segment)
        {
            if (segment == null)
                throw new ArgumentNullException(nameof(segment));

            var memorySegment = segment.AsMemory().Trim();

            if (memorySegment.IsEmpty)
            {
                throw new ArgumentException("The argument must neither be empty nor consist of whitespace only.", nameof(segment));
            }

            Segment = memorySegment;
            EscapedSegment = Escape(memorySegment);
        }

        public CoordinationEntryPathSegment(ReadOnlyMemory<char> segment)
        {
            segment = segment.Trim();

            if (segment.IsEmpty)
            {
                throw new ArgumentException("The argument must neither be empty nor consist of whitespace only.", nameof(segment));
            }

            Segment = segment;
            EscapedSegment = Escape(segment);
        }

        private CoordinationEntryPathSegment(ReadOnlyMemory<char> segment, ReadOnlyMemory<char> escapedSegment)
        {
            Segment = segment;
            EscapedSegment = escapedSegment;
        }

        public ReadOnlyMemory<char> Segment { get; }
        public ReadOnlyMemory<char> EscapedSegment { get; }

        private static ReadOnlyMemory<char> Escape(ReadOnlyMemory<char> unescapedSegment)
        {
            int CountCharsToEscape()
            {
                var str = unescapedSegment.Span;

                var count = 0;

                for (var i = 0; i < str.Length; i++)
                {
                    switch (str[i])
                    {
                        case CoordinationEntryPath.PathDelimiter:
                        case CoordinationEntryPath.AltPathDelimiter:
                        case EscapeChar:
                            count++;
                            break;
                    }
                }

                return count;
            }

            var numberOfCharsToEscape = CountCharsToEscape();

            if (numberOfCharsToEscape == 0)
            {
                return unescapedSegment;
            }
            var span = unescapedSegment.Span;
            var result = MemoryMarshal.AsMemory(new string('\0', count: unescapedSegment.Length + numberOfCharsToEscape).AsMemory());
            var resultWriter = new MemoryWriter<char>(result);

            var copySegmentStart = 0;

            for (var i = 0; i < unescapedSegment.Length; i++)
            {
                void PrepareEscape()
                {
                    var numberOfCharsToCopy = i - copySegmentStart;
                    Assert(numberOfCharsToCopy >= 0);

                    if (numberOfCharsToCopy > 0)
                    {
                        // Copy from unescapedSegment string all chars from (including) copySegmentStart to i (excluding)
                        resultWriter.Append(unescapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                    }

                    // Append an escape char
                    resultWriter.Append(EscapeChar);
                }

                switch (span[i])
                {
                    case CoordinationEntryPath.PathDelimiter:
                        PrepareEscape();
                        resultWriter.Append(PathDelimiterReplacement);
                        // Set copySegmentStart to i
                        copySegmentStart = i + 1;
                        break;

                    case CoordinationEntryPath.AltPathDelimiter:
                        PrepareEscape();
                        resultWriter.Append(AltPathDelimiterReplacement);
                        // Set copySegmentStart to i
                        copySegmentStart = i + 1;
                        break;

                    case EscapeChar:
                        PrepareEscape();
                        // Set copySegmentStart to i
                        copySegmentStart = i;
                        break;
                }
            }

            void CopyRemainingChars()
            {
                var numberOfCharsToCopy = unescapedSegment.Length - copySegmentStart;
                Assert(numberOfCharsToCopy >= 0);

                if (numberOfCharsToCopy > 0)
                {
                    resultWriter.Append(unescapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                }
            }

            CopyRemainingChars();
            var memory = resultWriter.GetMemory();
            Assert(memory.Length == result.Length);
            return memory;
        }

        private static ReadOnlyMemory<char> Unescape(ReadOnlyMemory<char> escapedSegment)
        {
            var span = escapedSegment.Span;
            Memory<char>? result = null;
            MemoryWriter<char> resultWriter = default;

            var copySegmentStart = 0;
            var escapedCharHit = false;

            for (var i = 0; i < escapedSegment.Length; i++)
            {
                switch (span[i])
                {
                    case EscapeChar:
                        if (escapedCharHit)
                        {
                            // Set copySegmentStart to i, as we do not need to manually translate - to -
                            copySegmentStart = i;
                            escapedCharHit = false;
                            break;
                        }

                        if (result == null)
                        {
                            var allocatedMemory = MemoryMarshal.AsMemory(new string('\0', count: escapedSegment.Length).AsMemory());
                            result = allocatedMemory;
                            resultWriter = new MemoryWriter<char>(allocatedMemory);
                        }

                        var numberOfCharsToCopy = i - copySegmentStart;
                        Assert(numberOfCharsToCopy >= 0);
                        if (numberOfCharsToCopy > 0)
                        {
                            // Copy from escapedSegment string all chars from (including) copySegmentStart to i (excluding)
                            resultWriter.Append(escapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                        }

                        // There has to be a next char.
                        Assert(i + 1 < escapedSegment.Length);
                        escapedCharHit = true;
                        break;

                    case PathDelimiterReplacement:
                        if (escapedCharHit)
                        {
                            Assert(result != null);
                            resultWriter.Append(CoordinationEntryPath.PathDelimiter);

                            // Set copySegmentStart to i + 1, as we already translated X to / appended it
                            copySegmentStart = i + 1;
                            escapedCharHit = false;
                        }
                        break;

                    case AltPathDelimiterReplacement:
                        if (escapedCharHit)
                        {
                            Assert(result != null);
                            resultWriter.Append(CoordinationEntryPath.AltPathDelimiter);

                            // Set copySegmentStart to i + 1, as we already translated Y to \ appended it
                            copySegmentStart = i + 1;
                            escapedCharHit = false;
                        }
                        break;

                    case CoordinationEntryPath.PathDelimiter:
                    case CoordinationEntryPath.AltPathDelimiter:
                        throw new ArgumentException("An escaped segment must not contain a path delimiter.", nameof(escapedSegment));

                    default:
                        if (escapedCharHit)
                        {
                            throw new ArgumentException($"Unknown escape character '{span[i]}' in escaped segment.", nameof(escapedSegment));
                        }
                        break;
                }
            }

            ReadOnlyMemory<char> CopyRemainingChars()
            {
                if (copySegmentStart == 0)
                {
                    Assert(result == null);

                    return escapedSegment;
                }

                Assert(result != null);
                var numberOfCharsToCopy = escapedSegment.Length - copySegmentStart;
                Assert(numberOfCharsToCopy >= 0);
                if (numberOfCharsToCopy > 0)
                {
                    resultWriter.Append(escapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                }

                return resultWriter.GetMemory();
            }

            return CopyRemainingChars();
        }

        public bool Equals(CoordinationEntryPathSegment other)
        {
            return Segment.Span.SequenceEqual(other.Segment.Span);
        }

        public override bool Equals(object obj)
        {
            return obj is CoordinationEntryPathSegment segment && Equals(segment);
        }

        public override int GetHashCode()
        {
            return Segment.Span.SequenceHashCode();
        }

        public override string ToString()
        {
            return Segment.ConvertToString();
        }

        public static bool operator ==(CoordinationEntryPathSegment left, CoordinationEntryPathSegment right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(CoordinationEntryPathSegment left, CoordinationEntryPathSegment right)
        {
            return !left.Equals(right);
        }

        public static CoordinationEntryPathSegment FromEscapedSegment(ReadOnlyMemory<char> escapedSegment)
        {
            escapedSegment = escapedSegment.Trim();
            var unescapedSegment = Unescape(escapedSegment);
            return new CoordinationEntryPathSegment(unescapedSegment, escapedSegment);
        }
    }
}
