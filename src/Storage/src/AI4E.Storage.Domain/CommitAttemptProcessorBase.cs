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
using System.Collections.Immutable;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public abstract class CommitAttemptProcessorBase : ICommitAttemptProcessor
    {
        public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CommitAttemptProcessingStep nextProcessing,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            var commitAttemptEntries = ImmutableArray.CreateBuilder<CommitAttemptEntry>(
                initialCapacity: commitAttempt.Entries.Count);

            commitAttemptEntries.Count = commitAttempt.Entries.Count;

            var i = 0;
            foreach (var originalEntry in commitAttempt.Entries)
            {
                ref var entry = ref Unsafe.AsRef(in commitAttemptEntries.ItemRef(i++));

                ProcessEntry(originalEntry, out entry);
            }

            var commitAttemptEntryCollection = new CommitAttemptEntryCollection<CommitAttemptEntry>(
                commitAttemptEntries.MoveToImmutable());

            var processedCommitAttempt = new CommitAttempt<CommitAttemptEntry>(commitAttemptEntryCollection);
            return nextProcessing.ProcessCommitAttemptAsync(processedCommitAttempt);
        }

        protected abstract void ProcessEntry<TCommitAttemptEntry>(
            TCommitAttemptEntry entry,
            out CommitAttemptEntry result)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>;
    }
}
