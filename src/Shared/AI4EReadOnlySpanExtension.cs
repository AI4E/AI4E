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

using System.Diagnostics;

namespace System
{
    internal static class AI4EMemoryExtensions
    {
        /// <summary>
        /// Indicates whether the specified span is empty or contains only white-space characters.
        /// </summary>
        public static bool IsEmptyOrWhiteSpace(this ReadOnlySpan<char> span)
        {
            return span.IsEmpty || span.IsWhiteSpace();
        }

#if NETSTD20 || NETSTD21

        // TODO: Add other Trim methods from COREFX

        /// <summary>
        /// Removes all leading and trailing white-space characters from the memory.
        /// </summary>
        /// <param name="memory">The source memory from which the characters are removed.</param>
        public static ReadOnlyMemory<char> Trim(this ReadOnlyMemory<char> memory)
        {
            var span = memory.Span;
            var start = ClampStart(span);
            var length = ClampEnd(span, start);
            return memory.Slice(start, length);
        }

        /// <summary>
        /// Delimits all leading occurrences of whitespace charecters from the span.
        /// </summary>
        /// <param name="span">The source span from which the characters are removed.</param>
        private static int ClampStart(ReadOnlySpan<char> span)
        {
            var start = 0;

            for (; start < span.Length; start++)
            {
                if (!char.IsWhiteSpace(span[start]))
                {
                    break;
                }
            }

            return start;
        }

        /// <summary>
        /// Delimits all trailing occurrences of whitespace charecters from the span.
        /// </summary>
        /// <param name="span">The source span from which the characters are removed.</param>
        /// <param name="start">The start index from which to being searching.</param>
        private static int ClampEnd(ReadOnlySpan<char> span, int start)
        {
            // Initially, start==len==0. If ClampStart trims all, start==len
            Debug.Assert((uint)start <= span.Length);

            var end = span.Length - 1;

            for (; end >= start; end--)
            {
                if (!char.IsWhiteSpace(span[end]))
                {
                    break;
                }
            }

            return end - start + 1;
        }

#endif
    }
}
