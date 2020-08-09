/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2020 Andreas Truetschel and contributors.
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

using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public interface ICommitAttemptExecutor
    {
        /// <summary>
        /// Asynchronously commits the specified commit-attempt an dispatches all domain-events.
        /// </summary>
        /// <typeparam name="TCommitAttemptEntry">The type of commit-attempt entry.</typeparam>
        /// <param name="commitAttempt">The <see cref="CommitAttempt{TCommitAttemptEntry}"/> to commit.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation 
        /// or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="ValueTask{EntityCommitResult}"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the commit result indicating commit success 
        /// or failure information.
        /// </returns>
        ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>;
    }
}
