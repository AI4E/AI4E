/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IEventStream.cs 
 * Types:           (1) AI4E.Storage.IEventStream
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
    /// Indicates the ability to track a series of events and commit them to durable storage.
    /// </summary>
    /// <remarks>
    /// Instances of this class are single threaded and should not be shared between threads.
    /// </remarks>
    public interface IEventStream<TBucket, TStreamId>
        where TBucket : IEquatable<TBucket>
        where TStreamId : IEquatable<TStreamId>
    {
        /// <summary>
        /// Gets the value which identifies bucket to which the the stream belongs.
        /// </summary>
        TBucket BucketId { get; }
        /// <summary>
        /// Gets the value which uniquely identifies the stream to which the stream belongs.
        /// </summary>
        TStreamId StreamId { get; }

        /// <summary>
        /// Gets the value which indicates the most recent committed sequence identifier of the event stream.
        /// </summary>
        long StreamRevision { get; }

        Guid ConcurrencyToken { get; }

        IEnumerable<ICommit<TBucket, TStreamId>> Commits { get; }

        IReadOnlyList<EventMessage> Events { get; }
        IReadOnlyDictionary<string, object> Headers { get; }

        ISnapshot<TBucket, TStreamId> Snapshot { get; }

        Task<Guid> CommitAsync(Guid concurrencyToken,
                               IEnumerable<EventMessage> events,
                               object body,
                               Action<IDictionary<string, object>> headerGenerator,
                               CancellationToken cancellation = default);

        Task<bool> UpdateAsync(CancellationToken cancellation);

        /// <summary>
        /// Asynchronously adds a snapshot for the current stream revision.
        /// </summary>
        /// <param name="body">The body that is a snapshot of the commits till the current stream revision.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        Task AddSnapshotAsync(object body, CancellationToken cancellation = default);
    }
}