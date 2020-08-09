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
        private readonly IEntityStorageEngine _storageEngine;

        public CommitAttemptProcessingQueue(IEntityStorageEngine storageEngine) 
            : this(storageEngine, Enumerable.Empty<ICommitAttemptProcessorRegistration>()) { }

        public CommitAttemptProcessingQueue(
            IEntityStorageEngine storageEngine, 
            IEnumerable<ICommitAttemptProcessorRegistration> processorRegistrations)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));
            if (processorRegistrations is null)
                throw new ArgumentNullException(nameof(processorRegistrations));

            _processorRegistrations = processorRegistrations.Reverse().ToImmutableList();
            _storageEngine = storageEngine;
        }

        // TODO: Pooling

        public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            if (!_processorRegistrations.Any())
            {
                return _storageEngine.ProcessCommitAttemptAsync(commitAttempt, cancellation);
            }

            ICommitAttemptExecutor processing =_storageEngine;

            foreach (var processorRegistration in _processorRegistrations)
            {
                var processor = processorRegistration.CreateCommitAttemptProcessor(serviceProvider);
                processing = new CompiledCommitAttemptProcessor(processor, processing);
            }

            return processing.ProcessCommitAttemptAsync(commitAttempt, cancellation);
        }

        private sealed class CompiledCommitAttemptProcessor : ICommitAttemptExecutor
        {
            private readonly ICommitAttemptProcessor _processor;
            private readonly ICommitAttemptExecutor _nextProcessing;

            public CompiledCommitAttemptProcessor(
                ICommitAttemptProcessor processor,
                ICommitAttemptExecutor nextProcessing)
            {
                _processor = processor;
                _nextProcessing = nextProcessing;
            }

            public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
                CommitAttempt<TCommitAttemptEntry> commitAttempt,
                CancellationToken cancellation)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                var nextProcessingStep = new CommitAttemptProcessingStep(_nextProcessing, cancellation);
                return _processor.ProcessCommitAttemptAsync(commitAttempt, nextProcessingStep, cancellation);
            }
        }
    }
}
