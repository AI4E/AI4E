/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IStreamStore.cs 
 * Types:           AI4E.Storage.Domain.IStreamStore
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   13.06.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * NEventStore (https://github.com/NEventStore/NEventStore)
 * The MIT License
 * 
 * Copyright (c) 2013 Jonathan Oliver, Jonathan Matheus, Damian Hickey and contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a store of streams.
    /// </summary>
    /// <remarks> This type is not thread-safe. </remarks>
    public interface IStreamStore : IDisposable
    {
        /// <summary>
        /// Asynchronously opens a stream.
        /// </summary>
        /// <param name="bucketId">The id of the bucket, the stream belongs to.</param>
        /// <param name="streamId">The id of the stream.</param>
        /// <param name="throwIfNotFound">A boolean value indicating whether an exception shall be throw if the stream does not exist.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the stream.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="bucketId"/> or <paramref name="streamId"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        /// <exception cref="StreamNotFoundException">Thrown if the stream cannot be found and <paramref name="throwIfNotFound"/> is true.</exception>
        Task<IStream> OpenStreamAsync(string bucketId, string streamId, bool throwIfNotFound = false, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously opens a stream within the specified revision and returns a read-only view.
        /// </summary>
        /// <param name="bucketId">The id of the bucket, the stream belongs to.</param>
        /// <param name="streamId">The id of the stream.</param>
        /// <param name="revision">The revision of the to be openend stream.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains the read-only view of the stream.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if either <paramref name="bucketId"/> or <paramref name="streamId"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        /// <exception cref="StreamNotFoundException">Thrown if the stream either cannot be found or cannot be opened within the specified revision.</exception>
        Task<IStream> OpenStreamAsync(string bucketId, string streamId, long revision, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously enumerates over all streams from the specified bucket.
        /// </summary>
        /// <param name="bucketId">The id of the bucket, the streams belong to.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An asynchronous enumerable that enumerates over all streams of the specified bucket.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="bucketId"/> is null.</exception>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        IAsyncEnumerable<IStream> OpenAllAsync(string bucketId, CancellationToken cancellation);

        /// <summary>
        /// Asynchronously enumerates over all streams.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An asynchronous enumerable that enumerates over all streams.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        IAsyncEnumerable<IStream> OpenAllAsync(CancellationToken cancellation);

        /// <summary>
        /// Asynchronously enumerates over all streams that needs a snapshot to be taken of.
        /// </summary>
        /// <param name="maxThreshold">The number of commits that a stream must contain since the latest snapshot.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An asynchronous enumerable that enumerates over all streams a snapshot to be taken of.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        /// <exception cref="ArgumentOutOfRangeException">Thrown if <paramref name="maxThreshold"/> is less than or equal zero.</exception>
        IAsyncEnumerable<IStream> OpenStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation);
    }
}