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

using System.Buffers;
using System.Linq.Expressions;
using System.Reflection;
using static System.Diagnostics.Debug;

namespace System
{
    public static class AI4EUtilsMemoryCompatibilityRandomExtensions
    {
        private static readonly NextBytesShim? _nextBytesShim= BuildNextBytesShim(typeof(Random));

        private static NextBytesShim? BuildNextBytesShim(Type randomType)
        {
            var nextBytesMethod = randomType.GetMethod(nameof(Random.NextBytes),
                                                       BindingFlags.Instance | BindingFlags.Public,
                                                       Type.DefaultBinder,
                                                       new Type[] { typeof(Span<byte>) },
                                                       modifiers: null);

            if (nextBytesMethod == null)
                return null;

            Assert(nextBytesMethod.ReturnType == typeof(void));

            var randomParameter = Expression.Parameter(typeof(Random), "random");
            var bufferParameter = Expression.Parameter(typeof(Span<byte>), "buffer");
            var call = Expression.Call(randomParameter, nextBytesMethod, bufferParameter);
            var lambda = Expression.Lambda<NextBytesShim>(call, randomParameter, bufferParameter);

            return lambda.Compile();
        }

        private delegate void NextBytesShim(Random random, Span<byte> buffer);

        public static void NextBytes(this Random random, Span<byte> buffer)
        {
            if (random == null)
                throw new ArgumentNullException(nameof(random));

            if (_nextBytesShim != null)
            {
                _nextBytesShim(random, buffer);
                return;
            }

            var array = ArrayPool<byte>.Shared.Rent(buffer.Length);

            try
            {
                random.NextBytes(array);

                array.AsSpan(start: 0, length: buffer.Length).CopyTo(buffer);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(array);
            }
        }
    }
}
