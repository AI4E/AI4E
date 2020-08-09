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
    public interface ICommitAttemptProcessor
    {
        ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            CommitAttemptProcessingStep nextProcessing,
            CancellationToken cancellation = default)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>;
    }

#pragma warning disable CA1815
    public readonly struct CommitAttemptProcessingStep
#pragma warning restore CA1815
    {
        private readonly ICommitAttemptExecutor? _processing;
        private readonly IServiceProvider _serviceProvider;
        private readonly CancellationToken _cancellation;

        public CommitAttemptProcessingStep(
            ICommitAttemptExecutor processing,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
        {
            if (processing is null)
                throw new ArgumentNullException(nameof(processing));

            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _processing = processing;
            _serviceProvider = serviceProvider;
            _cancellation = cancellation;
        }

        public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            if (_processing is null)
                return new ValueTask<EntityCommitResult>(EntityCommitResult.CommitProcessingFailure);

            return _processing.ProcessCommitAttemptAsync(commitAttempt, _serviceProvider, _cancellation);
        }
    }
}
