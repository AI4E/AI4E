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

#pragma warning disable CA2225

using System;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;

namespace AI4E.Utils
{
    [StructLayout(LayoutKind.Sequential, Size = 4)]
    public readonly struct SeqNum : IEquatable<SeqNum>, IComparable<SeqNum>
    {
        public SeqNum(int rawValue)
        {
            RawValue = rawValue;
        }

#pragma warning disable CA1822
        public int RawValue { [MethodImpl(MethodImplOptions.AggressiveInlining)] get; }
#pragma warning restore CA1822

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public int CompareTo(SeqNum other)
        {
            return unchecked(-(other.RawValue - RawValue));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public bool Equals(SeqNum other)
        {
            return other.RawValue == RawValue;
        }

        public override bool Equals(object? obj)
        {
            return obj is SeqNum seqNum && Equals(seqNum);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override int GetHashCode()
        {
            return RawValue.GetHashCode();
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public override string ToString()
        {
#pragma warning disable CA1305
            return RawValue.ToString();
#pragma warning restore CA1305
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public string ToString(IFormatProvider formatProvider)
        {
            return RawValue.ToString(formatProvider);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator ==(SeqNum left, SeqNum right)
        {
            return left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator !=(SeqNum left, SeqNum right)
        {
            return !left.Equals(right);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <(SeqNum left, SeqNum right)
        {
            return left.CompareTo(right) < 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >(SeqNum left, SeqNum right)
        {
            return left.CompareTo(right) > 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator <=(SeqNum left, SeqNum right)
        {
            return left.CompareTo(right) <= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static bool operator >=(SeqNum left, SeqNum right)
        {
            return left.CompareTo(right) >= 0;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator int(SeqNum seqNum)
        {
            return seqNum.RawValue;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static explicit operator SeqNum(int rawValue)
        {
            return new SeqNum(rawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum operator +(SeqNum seqNum, int value)

        {
            return new SeqNum(unchecked(seqNum.RawValue + value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum operator +(int value, SeqNum seqNum)
        {
            return new SeqNum(unchecked(seqNum.RawValue + value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum operator -(SeqNum seqNum, int value)
        {
            return new SeqNum(unchecked(seqNum.RawValue - value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum operator -(int value, SeqNum seqNum)
        {
            return new SeqNum(unchecked(seqNum.RawValue - value));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static int operator -(SeqNum left, SeqNum right)
        {
            return unchecked(left.RawValue - right.RawValue);
        }
    }

    public static class SeqNumInterlocked
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum Add(ref SeqNum location, int value)
        {
            return (SeqNum)Interlocked.Add(ref Reinterpret(ref location), value);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum Increment(ref SeqNum location)
        {
            return (SeqNum)Interlocked.Increment(ref Reinterpret(ref location));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum Decrement(ref SeqNum location)
        {
            return (SeqNum)Interlocked.Decrement(ref Reinterpret(ref location));
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum Exchange(ref SeqNum location, SeqNum value)
        {
            return (SeqNum)Interlocked.Exchange(ref Reinterpret(ref location), value.RawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public static SeqNum CompareExchange(ref SeqNum location, SeqNum value, SeqNum comparand)
        {
            return (SeqNum)Interlocked.CompareExchange(ref Reinterpret(ref location), value.RawValue, comparand.RawValue);
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static ref int Reinterpret(ref SeqNum location)
        {
            return ref Unsafe.As<SeqNum, int>(ref location);
        }
    }
}

#pragma warning restore CA2225
