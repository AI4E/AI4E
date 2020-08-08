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
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public sealed class CommitAttemptProcessingQueue : ICommitAttemptProccesingQueue
    {
        private readonly ImmutableList<ICommitAttemptProcessorRegistration> _processorRegistrations;

        public CommitAttemptProcessingQueue() : this(Enumerable.Empty<ICommitAttemptProcessorRegistration>()) { }

        public CommitAttemptProcessingQueue(IEnumerable<ICommitAttemptProcessorRegistration> processorRegistrations)
        {
            if (processorRegistrations is null)
                throw new ArgumentNullException(nameof(processorRegistrations));

            _processorRegistrations = processorRegistrations.Reverse().ToImmutableList();
        }

        // TODO: Pooling

        public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            IEntityStorageEngine storageEngine,
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (!_processorRegistrations.Any())
            {
                return storageEngine.CommitAsync(commitAttempt, cancellation);
            }

            ICommitAttemptProcessing processing = new StorageEngineCommitAttemptProcessing(
                storageEngine, cancellation);

            foreach (var processorRegistration in _processorRegistrations)
            {
                var processor = processorRegistration.CreateCommitAttemptProcessor(serviceProvider);
                processing = new ProcessorCommitAttemptProcessing(processor, processing, cancellation);
            }

            return processing.ProcessCommitAttemptAsync(commitAttempt);
        }

        private sealed class StorageEngineCommitAttemptProcessing : ICommitAttemptProcessing
        {
            private readonly IEntityStorageEngine _storageEngine;
            private readonly CancellationToken _cancellation;

            public StorageEngineCommitAttemptProcessing(
                IEntityStorageEngine storageEngine,
                CancellationToken cancellation)
            {
                _storageEngine = storageEngine;
                _cancellation = cancellation;
            }

            public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
                CommitAttempt<TCommitAttemptEntry> commitAttempt)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                return _storageEngine.CommitAsync(commitAttempt, _cancellation);
            }
        }

        private sealed class ProcessorCommitAttemptProcessing : ICommitAttemptProcessing
        {
            private readonly ICommitAttemptProcessor _processor;
            private readonly ICommitAttemptProcessing _nextProcessing;
            private readonly CancellationToken _cancellation;

            public ProcessorCommitAttemptProcessing(
                ICommitAttemptProcessor processor,
                ICommitAttemptProcessing nextProcessing,
                CancellationToken cancellation)
            {
                _processor = processor;
                _nextProcessing = nextProcessing;
                _cancellation = cancellation;
            }

            public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
                CommitAttempt<TCommitAttemptEntry> commitAttempt)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                return _processor.ProcessCommitAttemptAsync(commitAttempt, _nextProcessing, _cancellation);
            }
        }
    }
}
