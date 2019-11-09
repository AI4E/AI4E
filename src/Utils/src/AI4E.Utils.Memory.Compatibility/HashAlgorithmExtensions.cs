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

namespace System.Security.Cryptography
{
    public static class AI4EUtilsMemoryCompatibilityHashAlgorithmExtensions
    {
        private static readonly TryComputeHashShim? _tryComputeHashShim= BuildTryComputeHashShim(typeof(HashAlgorithm));

        private static TryComputeHashShim? BuildTryComputeHashShim(Type hashAlgorithmType)
        {
            var tryCompateHashMethod = hashAlgorithmType.GetMethod("TryComputeHash",
                                                                   BindingFlags.Instance | BindingFlags.Public,
                                                                   Type.DefaultBinder,
                                                                   new Type[] { typeof(ReadOnlySpan<byte>), typeof(Span<byte>), typeof(int).MakeByRefType() },
                                                                   modifiers: null);

            if (tryCompateHashMethod == null)
                return null;

            Assert(tryCompateHashMethod.ReturnType == typeof(bool));

            var hashAlgorithmParameter = Expression.Parameter(hashAlgorithmType, "hashAlgorithm");
            var sourceParameter = Expression.Parameter(typeof(ReadOnlySpan<byte>), "source");
            var destinationParameter = Expression.Parameter(typeof(Span<byte>), "destination");
            var bytesWrittenParameter = Expression.Parameter(typeof(int).MakeByRefType(), "bytesWritten");
            var call = Expression.Call(hashAlgorithmParameter, tryCompateHashMethod, sourceParameter, destinationParameter, bytesWrittenParameter);
            var lambda = Expression.Lambda<TryComputeHashShim>(call, hashAlgorithmParameter, sourceParameter, destinationParameter, bytesWrittenParameter);
            return lambda.Compile();
        }

        private delegate bool TryComputeHashShim(HashAlgorithm hashAlgorithm, ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten);

        public static bool TryComputeHash(this HashAlgorithm hashAlgorithm, ReadOnlySpan<byte> source, Span<byte> destination, out int bytesWritten)
        {
            if (hashAlgorithm == null)
                throw new ArgumentNullException(nameof(hashAlgorithm));

            if (_tryComputeHashShim != null)
            {
                return _tryComputeHashShim(hashAlgorithm, source, destination, out bytesWritten);
            }

            byte[] destinationArray;

            var sourceArray = ArrayPool<byte>.Shared.Rent(source.Length);
            try
            {
                source.CopyTo(sourceArray.AsSpan().Slice(start: 0, length: source.Length));

                destinationArray = hashAlgorithm.ComputeHash(sourceArray, offset: 0, count: source.Length);
            }
            finally
            {
                ArrayPool<byte>.Shared.Return(sourceArray);
            }

            if (destinationArray.Length > destination.Length)
            {
                bytesWritten = 0;
                return false;
            }

            destinationArray.AsSpan().CopyTo(destination.Slice(start: 0, length: destinationArray.Length));
            bytesWritten = destinationArray.Length;
            return true;
        }
    }
}
