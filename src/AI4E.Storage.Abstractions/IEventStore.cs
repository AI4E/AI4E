/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IEventStore.cs 
 * Types:           (1) AI4E.Storage.IEventStore
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   08.01.2018 
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

namespace AI4E.Storage
{
    /// <summary>
    /// Indicates the ability to store and retreive a stream of events.
    /// </summary>
    /// <remarks>
    /// Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
    /// </remarks>
    public interface IEventStore<TBucket, TStreamId> : IDisposable
        where TBucket : IEquatable<TBucket>
        where TStreamId : IEquatable<TStreamId>
    {
        /// <summary>
        /// Reads the stream indicated from the minimum revision specified up to the maximum revision specified or creates
        /// an empty stream if no commits are found and a minimum revision of zero is provided.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="streamId">The value which uniquely identifies the stream in the bucket from which the events will be read.</param>
        /// <param name="minRevision">The minimum revision of the stream to be read.</param>
        /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
        /// <returns>A series of committed events represented as a stream.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        /// <exception cref="StreamNotFoundException" />
        Task<IEventStream<TBucket, TStreamId>> OpenStreamAsync(TBucket bucketId, TStreamId streamId, CancellationToken cancellation = default);

        Task<IEventStream<TBucket, TStreamId>> OpenStreamAsync(TBucket bucketId, TStreamId streamId, long revision, CancellationToken cancellation = default);

        Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenAllAsync(TBucket bucketId, CancellationToken cancellation);

        Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenAllAsync(CancellationToken cancellation);

        Task<IEnumerable<IEventStream<TBucket, TStreamId>>> OpenStreamsToSnapshotAsync(long maxThreshold, CancellationToken cancellation);
    }
}