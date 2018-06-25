/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IStreamPersistence.cs 
 * Types:           (1) AI4E.Storage.Domain.IStreamPersistence
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
    /// Indicates the ability to adapt the underlying persistence infrastructure to behave like a stream of events.
    /// </summary>
    /// <remarks>
    /// Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
    /// </remarks>
    public interface IStreamPersistence : IDisposable
    {
        /// <summary>
        /// Gets a value indicating whether this instance has been disposed of.
        /// </summary>
        bool IsDisposed { get; }

        /// <summary>
        /// Gets the most recent snapshot which was taken on or before the revision indicated.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="streamId">The stream to be searched for a snapshot.</param>
        /// <param name="maxRevision">The maximum revision possible for the desired snapshot.</param>
        /// <returns>If found, it returns the snapshot; otherwise null is returned.</returns>
        /// <exception cref="ArgumentNullException">Throw if either <paramref name="bucketId"/> or <paramref name="streamId"/> is null.</exception>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task<ISnapshot> GetSnapshotAsync(string bucketId, string streamId, long maxRevision = default, CancellationToken cancellation = default);

        IAsyncEnumerable<ISnapshot> GetSnapshotsAsync(string bucketId, CancellationToken cancellation = default);

        IAsyncEnumerable<ISnapshot> GetSnapshotsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Adds the snapshot provided to the stream indicated.
        /// </summary>
        /// <param name="snapshot">The snapshot to save.</param>
        /// <returns>If the snapshot was added, returns true; otherwise false.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task<bool> AddSnapshotAsync(ISnapshot snapshot, CancellationToken cancellation = default);

        /// <summary>
        /// Gets identifiers for all streams whose head revision differs from its last snapshot revision by at least the threshold specified.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="maxThreshold">The maximum difference between the head and most recent snapshot revisions.</param>
        /// <returns>The streams for which the head and snapshot revisions differ by at least the threshold specified.</returns>
        /// <exception cref="ArgumentNullException">Throw if <paramref name="bucketId"/> is null.</exception>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        IAsyncEnumerable<IStreamHead> GetStreamsToSnapshotAsync(string bucketId,
                                                                long maxThreshold,
                                                                CancellationToken cancellation = default);

        IAsyncEnumerable<IStreamHead> GetStreamsToSnapshotAsync(long maxThreshold,
                                                                CancellationToken cancellation = default);

        /// <summary>
        /// Gets the corresponding commits from the stream indicated starting at the revision specified until the
        /// end of the stream sorted in ascending order--from oldest to newest.
        /// </summary>
        /// <param name="bucketId">The value which uniquely identifies bucket the stream belongs to.</param>
        /// <param name="streamId">The stream from which the events will be read.</param>
        /// <param name="minRevision">The minimum revision of the stream to be read.</param>
        /// <param name="maxRevision">The maximum revision of the stream to be read.</param>
        /// <returns>A series of committed events from the stream specified sorted in ascending order.</returns>
        /// <exception cref="ArgumentNullException">Throw if either <paramref name="bucketId"/> or <paramref name="streamId"/> is null.</exception>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        IAsyncEnumerable<ICommit> GetCommitsAsync(string bucketId,
                                                  string streamId,
                                                  long minRevision = default,
                                                  long maxRevision = default,
                                                  CancellationToken cancellation = default);

        IAsyncEnumerable<ICommit> GetCommitsAsync(string bucketId, CancellationToken cancellation = default);

        IAsyncEnumerable<ICommit> GetCommitsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Writes the to-be-commited events provided to the underlying persistence mechanism.
        /// </summary>
        /// <param name="attempt">The series of events and associated metadata to be commited.</param>
        /// <exception cref="ConcurrencyException" />
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task<ICommit> CommitAsync(CommitAttempt attempt, CancellationToken cancellation = default);

        /// <summary>
        /// Gets a set of commits that has not yet been dispatched.
        /// </summary>
        /// <returns>The set of commits to be dispatched.</returns>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        IAsyncEnumerable<ICommit> GetUndispatchedCommitsAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Marks the commit specified as dispatched.
        /// </summary>
        /// <param name="commit">The commit to be marked as dispatched.</param>
        /// <exception cref="StorageException" />
        /// <exception cref="StorageUnavailableException" />
        Task MarkCommitAsDispatchedAsync(ICommit commit, CancellationToken cancellation = default);

        /// <summary>
        /// Deletes a stream.
        /// </summary>
        /// <param name="bucketId">The bucket Id from which the stream is to be deleted.</param>
        /// <param name="streamId">The stream Id of the stream that is to be deleted.</param>
        /// <exception cref="ArgumentNullException">Throw if either <paramref name="bucketId"/> or <paramref name="streamId"/> is null.</exception>
        Task DeleteStreamAsync(string bucketId, string streamId, CancellationToken cancellation = default);

        IAsyncEnumerable<IStreamHead> GetStreamHeadsAsync(string bucketId, CancellationToken cancellation = default);
        IAsyncEnumerable<IStreamHead> GetStreamHeadsAsync(CancellationToken cancellation = default);
    }
}