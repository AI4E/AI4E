using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public readonly struct CoordinationEntryPath : IEquatable<CoordinationEntryPath>
    {
        public const char PathDelimiter = '/';
        public const char AltPathDelimiter = '\\';
        private const string _pathDelimiterString = "/";

        private readonly ImmutableArray<CoordinationEntryPathSegment> _segments;

        public CoordinationEntryPath(params CoordinationEntryPathSegment[] segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

            _segments = segments.ToImmutableArray();
        }

        public CoordinationEntryPath(IEnumerable<CoordinationEntryPathSegment> segments)
        {
            if (segments == null)
                throw new ArgumentNullException(nameof(segments));

            if (segments.Any(p => p == default))
                throw new ArgumentException("The collection must not contain default entries.", nameof(segments));

            _segments = segments.ToImmutableArray();
        }

        public ImmutableArray<CoordinationEntryPathSegment> Segments => _segments.IsDefault ? ImmutableArray<CoordinationEntryPathSegment>.Empty : _segments;

        public string Path
        {
            get
            {
                if (!_segments.Any())
                {
                    return _pathDelimiterString;
                }

                var capacity = 1;

                for (var i = 0; i < _segments.Length; i++)
                {
                    capacity += _segments[i].EscapedSegment.Length + 1;
                }

                var resultsBuilder = new StringBuilder(capacity);

                resultsBuilder.Append(PathDelimiter);

                for (var i = 0; i < _segments.Length; i++)
                {
                    resultsBuilder.Append(_segments[i].EscapedSegment);
                    resultsBuilder.Append(PathDelimiter);
                }

                return resultsBuilder.ToString();
            }
        }

        public bool Equals(CoordinationEntryPath other)
        {
            if (Segments.Length != other.Segments.Length)
                return false;

            for (var i = 0; i < Segments.Length; i++)
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
            return Segments.GetHashCode(); // TODO: Can we use this?
        }

        public override string ToString()
        {
            if (!_segments.Any())
            {
                return "/";
            }

            var capacity = 1;

            for (var i = 0; i < _segments.Length; i++)
            {
                capacity += _segments[i].Segment.Length + 1;
            }

            var resultsBuilder = new StringBuilder(capacity);

            resultsBuilder.Append(PathDelimiter);

            for (var i = 0; i < _segments.Length; i++)
            {
                resultsBuilder.Append(_segments[i]);
                resultsBuilder.Append(PathDelimiter);
            }

            return resultsBuilder.ToString();
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

            void ProcessSegment(int end)
            {
                var spanX = path.Span;
                var whitespaceOnly = true;

                for (var j = segmentStart; j < end; j++)
                {
                    if (!char.IsWhiteSpace(spanX[j]))
                        whitespaceOnly = false;
                }

                if (!whitespaceOnly)
                {
                    var segment = CoordinationEntryPathSegment.FromEscapedSegment(path.Slice(segmentStart, end - segmentStart));
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
    }

    public readonly struct CoordinationEntryPathSegment : IEquatable<CoordinationEntryPathSegment>
    {
        public const char EscapeChar = '-';
        public const char PathDelimiterReplacement = 'X';
        public const char AltPathDelimiterReplacement = 'Y';

        public CoordinationEntryPathSegment(ReadOnlyMemory<char> segment)
        {
            segment = Trim(segment);

            if (segment.IsEmpty)
            {
                throw new ArgumentException("The argument must neither be empty nor consist of whitespace only.", nameof(segment));
            }

            Segment = segment;
            EscapedSegment = Escape(segment);
        }

        private static ReadOnlyMemory<char> Trim(ReadOnlyMemory<char> s)
        {
            if (s.IsEmpty)
                return s;

            var span = s.Span;
            var start = 0;

            for (; start < s.Length; start++)
            {
                if (!char.IsWhiteSpace(span[start]))
                {
                    break;
                }
            }

            if (start == s.Length)
            {
                return ReadOnlyMemory<char>.Empty;
            }

            var count = 1;

            for (; count + start < s.Length; count++)
            {
                if (char.IsWhiteSpace(span[count]))
                {
                    break;
                }
            }

            return s.Slice(start, count);
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
            var result = new char[unescapedSegment.Length + numberOfCharsToEscape];
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
            char[] result = null;
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

                        var numberOfCharsToCopy = i - copySegmentStart;
                        Assert(numberOfCharsToCopy >= 0);
                        if (numberOfCharsToCopy > 0)
                        {
                            if (result == null)
                            {
                                result = new char[escapedSegment.Length];
                                resultWriter = new MemoryWriter<char>(result);
                            }

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

                    default:
                        Assert(!escapedCharHit);
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
            var span = Segment.Span;
            var otherSpan = other.Segment.Span;

            if (span.IsEmpty)
            {
                return otherSpan.IsEmpty;
            }

            if (otherSpan.IsEmpty)
                return false;

            if (span.Length != otherSpan.Length)
                return false;

            for (var i = 0; i < span.Length; i++)
            {
                if (span[i] != otherSpan[i])
                    return false;
            }

            return true;
        }

        public override bool Equals(object obj)
        {
            return obj is CoordinationEntryPathSegment segment && Equals(segment);
        }

        public override int GetHashCode()
        {
            return Segment.GetHashCode();
        }

        public override string ToString()
        {
            return Segment.Span.ToString(); // TODO: Is this correct?
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
            escapedSegment = Trim(escapedSegment);

            var unescapedSegment = Unescape(escapedSegment);

            return new CoordinationEntryPathSegment(unescapedSegment, escapedSegment);
        }
    }

    internal struct MemoryWriter<T>
    {
        private readonly Memory<T> _memory;
        private int _position;

        public MemoryWriter(Memory<T> memory)
        {
            _memory = memory;
            _position = 0;
        }

        public void Append(ReadOnlySpan<T> span)
        {
            var spanX = _memory.Span;

            Assert(_position + span.Length <= _memory.Length);
            span.CopyTo(spanX.Slice(_position));
            _position += span.Length;
        }

        public void Append(T c)
        {
            var span = _memory.Span;

            Assert(_position + 1 <= _memory.Length);
            span[_position] = c;
            _position += 1;
        }

        public Memory<T> GetMemory()
        {
            return _memory.Slice(0, _position);
        }
    }
}
