/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IStorageExtension.cs 
 * Types:           (1) AI4E.Storage.IStorageExtension
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.01.2018 
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

namespace AI4E.Storage
{
    /// <summary>
    /// Provides the ability to hook into the pipeline of persisting a commit.
    /// </summary>
    /// <remarks>
    /// Instances of this class must be designed to be multi-thread safe such that they can be shared between threads.
    /// </remarks>
    public interface IStorageExtension<TBucketId, TStreamId> : IDisposable
        where TBucketId : IEquatable<TBucketId>
        where TStreamId : IEquatable<TStreamId>
    {
        /// <summary>
        /// Hooks into the selection pipeline just prior to the commit being returned to the caller.
        /// </summary>
        /// <param name="commit">The commit to be filtered.</param>
       void OnLoad(ICommit<TBucketId, TStreamId> commit);

        /// <summary>
        /// Hooks into the commit pipeline prior to persisting the commit to durable storage.
        /// </summary>
        /// <param name="attempt">The attempt to be committed.</param>
        /// <returns>If processing should continue, returns true; otherwise returns false.</returns>
        bool OnCommit(CommitAttempt<TBucketId, TStreamId> attempt);

        /// <summary>
        /// Hooks into the commit pipeline just after the commit has been *successfully* committed to durable storage.
        /// </summary>
        /// <param name="commit">The commit which has been persisted.</param>
        void OnCommited(ICommit<TBucketId, TStreamId> commit);

        /// <summary>
        /// Invoked when a stream has been deleted.
        /// </summary>
        /// <param name="bucketId">The bucket Id from which the stream whch has been deleted.</param>
        /// <param name="streamId">The stream Id of the stream which has been deleted.</param>
        void OnStreamDeleted(TBucketId bucketId, TStreamId streamId);
    }
}