/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IStream.cs 
 * Types:           AI4E.Storage.IStream
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   15.03.2018 
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
    /// Represents a sequence (or stream) of commits.
    /// </summary>
    /// <remarks> This type is not thread-safe. </remarks>
    public interface IStream<TBucketId, TStreamId>
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        /// <summary>
        /// Gets the value which identifies the bucket to which the stream belongs.
        /// </summary>
        TBucketId BucketId { get; }

        /// <summary>
        /// Gets the value which uniquely identifies the stream.
        /// </summary>
        TStreamId StreamId { get; }

        /// <summary>
        /// Gets the number of commits that the stream contains.
        /// </summary>
        long StreamRevision { get; }

        /// <summary>
        /// Gets the concurrency token to uniquely identify the most recent commit in the sequence.
        /// </summary>
        Guid ConcurrencyToken { get; }

        /// <summary>
        /// Gets a boolean value indicating whether this is a read-only view of the stream.
        /// </summary>
        bool IsReadOnly { get; }

        /// <summary>
        /// Gets the collection of commits, the stream consists of, started by the underlying snapshot.
        /// </summary>
        IEnumerable<ICommit<TBucketId, TStreamId>> Commits { get; }

        /// <summary>
        /// Gets the collection of events, the commits contains.
        /// </summary>
        IReadOnlyList<EventMessage> Events { get; }

        /// <summary>
        /// Gets the headers of the stream.
        /// </summary>
        IReadOnlyDictionary<string, object> Headers { get; }

        /// <summary>
        /// Gets the underlying snapshot of the view of the stream.
        /// </summary>
        ISnapshot<TBucketId, TStreamId> Snapshot { get; }

        /// <summary>
        /// Asynchronouly adds a commit to the sequence.
        /// </summary>
        /// <param name="concurrencyToken">The concurrency token used to ensure consistency.</param>
        /// <param name="events">The events, the commit contains.</param>
        /// <param name="body">The commit body.</param>
        /// <param name="headerGenerator">A function that generated the new stream headers from the existing.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation. 
        /// If evaluated, the tasks result contains the concurrency token of the generated commit.
        /// A return value of <see cref="Guid.Empty"/> indicates that no commit was generated.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this is a read-only stream view.</exception>
        /// <exception cref="ConcurrencyException">Thrown if a concurrency conflict occurs.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        Task<Guid> CommitAsync(Guid concurrencyToken,
                               IEnumerable<EventMessage> events,
                               object body,
                               Action<IDictionary<string, object>> headerGenerator,
                               CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously updates the view of the stream to the latest version.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// If evaluated, the tasks result contains a boolean value indicating whether the stream view changed.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this is a read-only stream view.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        Task<bool> UpdateAsync(CancellationToken cancellation);

        /// <summary>
        /// Asynchronously adds a snapshot for the current stream revision.
        /// </summary>
        /// <param name="body">The body that is a snapshot of the commits till the current stream revision.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="InvalidOperationException">Thrown if this is a read-only stream view.</exception>
        /// <exception cref="StorageException">Thrown if an exception occured in the storage system.</exception>
        Task AddSnapshotAsync(object body, CancellationToken cancellation = default);
    }
}