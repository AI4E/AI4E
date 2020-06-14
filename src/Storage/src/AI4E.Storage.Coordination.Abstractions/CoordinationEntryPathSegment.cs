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
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using AI4E.Utils.Memory;

namespace AI4E.Storage.Coordination
{
    /// <summary>
    /// Represents a segment in a coordination entry path.
    /// </summary>
    public readonly struct CoordinationEntryPathSegment : IEquatable<CoordinationEntryPathSegment>
    {
        private const char EscapeChar = '-';
        private const char PathDelimiterReplacement = 'X';
        private const char AltPathDelimiterReplacement = 'Y';

        /// <summary>
        /// Create a new <see cref="CoordinationEntryPathSegment"/> from the specified string.
        /// </summary>
        /// <param name="segment">A string that respresents the unescaped segment.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="segment"/> is <c>null</c>.</exception>
        public CoordinationEntryPathSegment(string segment) : this(segment?.AsMemory() ?? throw new ArgumentNullException(nameof(segment))) { }

        /// <summary>
        /// Create a new <see cref="CoordinationEntryPathSegment"/> from the specified char memory.
        /// </summary>
        /// <param name="segment">A memory of chars that respresents the unescaped segment.</param>
        public CoordinationEntryPathSegment(ReadOnlyMemory<char> segment)
        {
            segment = segment.Trim();

            if (segment.IsEmpty)
            {
                this = default;
                return;
            }

            Segment = segment;
            EscapedSegment = Escape(segment);
        }

        private CoordinationEntryPathSegment(ReadOnlyMemory<char> segment, ReadOnlyMemory<char> escapedSegment)
        {
            Segment = segment;
            EscapedSegment = escapedSegment;
        }

        /// <summary>
        /// Gets the memory of chars representing the unescaped segment.
        /// </summary>
        public ReadOnlyMemory<char> Segment { get; }

        /// <summary>
        /// Gets the memory of chars representing the escaped segment.
        /// </summary>
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
                        case CoordinationEntryPath._pathDelimiter:
                        case CoordinationEntryPath._altPathDelimiter:
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
                    Debug.Assert(numberOfCharsToCopy >= 0);

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
                    case CoordinationEntryPath._pathDelimiter:
                        PrepareEscape();
                        resultWriter.Append(PathDelimiterReplacement);
                        // Set copySegmentStart to i
                        copySegmentStart = i + 1;
                        break;

                    case CoordinationEntryPath._altPathDelimiter:
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
                Debug.Assert(numberOfCharsToCopy >= 0);

                if (numberOfCharsToCopy > 0)
                {
                    resultWriter.Append(unescapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                }
            }

            CopyRemainingChars();
            var memory = resultWriter.GetMemory();
            Debug.Assert(memory.Length == result.Length);
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
                        Debug.Assert(numberOfCharsToCopy >= 0);
                        if (numberOfCharsToCopy > 0)
                        {
                            // Copy from escapedSegment string all chars from (including) copySegmentStart to i (excluding)
                            resultWriter.Append(escapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                        }

                        // There has to be a next char.
                        Debug.Assert(i + 1 < escapedSegment.Length);
                        escapedCharHit = true;
                        break;

                    case PathDelimiterReplacement:
                        if (escapedCharHit)
                        {
                            Debug.Assert(result != null);
                            resultWriter.Append(CoordinationEntryPath._pathDelimiter);

                            // Set copySegmentStart to i + 1, as we already translated X to / appended it
                            copySegmentStart = i + 1;
                            escapedCharHit = false;
                        }
                        break;

                    case AltPathDelimiterReplacement:
                        if (escapedCharHit)
                        {
                            Debug.Assert(result != null);
                            resultWriter.Append(CoordinationEntryPath._altPathDelimiter);

                            // Set copySegmentStart to i + 1, as we already translated Y to \ appended it
                            copySegmentStart = i + 1;
                            escapedCharHit = false;
                        }
                        break;

                    case CoordinationEntryPath._pathDelimiter:
                    case CoordinationEntryPath._altPathDelimiter:
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
                    Debug.Assert(result == null);

                    return escapedSegment;
                }

                Debug.Assert(result != null);
                var numberOfCharsToCopy = escapedSegment.Length - copySegmentStart;
                Debug.Assert(numberOfCharsToCopy >= 0);
                if (numberOfCharsToCopy > 0)
                {
                    resultWriter.Append(escapedSegment.Slice(copySegmentStart, numberOfCharsToCopy).Span);
                }

                return resultWriter.GetMemory();
            }

            return CopyRemainingChars();
        }

        /// <inheritdoc/>
        public bool Equals(CoordinationEntryPathSegment other)
        {
            return Segment.Span.SequenceEqual(other.Segment.Span);
        }

        /// <inheritdoc/>
        public override bool Equals(object obj)
        {
            return obj is CoordinationEntryPathSegment segment && Equals(segment);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            return Segment.Span.SequenceHashCode();
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            return Segment.ConvertToString();
        }

        /// <summary>
        /// Compares two <see cref="CoordinationEntryPathSegment"/>s.
        /// </summary>
        /// <param name="left">The first segment.</param>
        /// <param name="right">The second segment.</param>
        /// <returns>True, if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(CoordinationEntryPathSegment left, CoordinationEntryPathSegment right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two <see cref="CoordinationEntryPathSegment"/>s.
        /// </summary>
        /// <param name="left">The first segment.</param>
        /// <param name="right">The second segment.</param>
        /// <returns>True, if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(CoordinationEntryPathSegment left, CoordinationEntryPathSegment right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Creates a <see cref="CoordinationEntryPathSegment"/> from the specified escaped memory of chars.
        /// </summary>
        /// <param name="escapedSegment">An escaped memory of chars representing the segment.</param>
        /// <returns>The <see cref="CoordinationEntryPathSegment"/> that was created from <paramref name="escapedSegment"/>.</returns>
        public static CoordinationEntryPathSegment FromEscapedSegment(ReadOnlyMemory<char> escapedSegment)
        {
            escapedSegment = escapedSegment.Trim();
            var unescapedSegment = Unescape(escapedSegment);
            return new CoordinationEntryPathSegment(unescapedSegment, escapedSegment);
        }
    }
}
