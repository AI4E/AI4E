﻿using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;

namespace AI4E.Coordination
{
    internal static class MemoryExtensions
    {
        public static ReadOnlyMemory<char> Trim(this ReadOnlyMemory<char> s)
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

        public static bool SequenceEquals<T>(this ReadOnlyMemory<T> left, ReadOnlyMemory<T> right, IEqualityComparer<T> comparer)
        {
            if (comparer == null)
                throw new ArgumentNullException(nameof(comparer));

            var leftSpan = left.Span;
            var rightSpan = right.Span;

            if (leftSpan.IsEmpty)
            {
                return rightSpan.IsEmpty;
            }

            if (rightSpan.IsEmpty)
                return false;

            if (leftSpan.Length != rightSpan.Length)
                return false;

            for (var i = 0; i < leftSpan.Length; i++)
            {
                if (!comparer.Equals(leftSpan[i], rightSpan[i]))
                    return false;
            }

            return true;
        }

        public static bool SequenceEquals<T>(this ReadOnlyMemory<T> left, ReadOnlyMemory<T> right)
        {
            return SequenceEquals(left, right, EqualityComparer<T>.Default);
        }

        public static bool IsEmptyOrWhiteSpace(this ReadOnlyMemory<char> s)
        {
            if (s.IsEmpty)
                return true;

            var span = s.Span;

            for (var j = 0; j < s.Length; j++)
            {
                if (!char.IsWhiteSpace(span[j]))
                {
                    return false;
                }
            }

            return true;
        }

        public static ReadOnlyMemory<T> Slice<T>(this ReadOnlyMemory<T> memory, int start, int exclusiveEnd)
        {
            return memory.Slice(start, exclusiveEnd - start);
        }

        public static string ConvertToString(this ReadOnlyMemory<char> memory)
        {
            var result = new string('\0', memory.Length);
            var resultAsMemory = MemoryMarshal.AsMemory(result.AsMemory());
            memory.CopyTo(resultAsMemory);
            return result;
        }
    }
}