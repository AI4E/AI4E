﻿/* License
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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public sealed class CommitAttemptProcessingQueue : ICommitAttemptProccesingQueue
    {
        private readonly ICommitAttemptExecutor _executor;

        public CommitAttemptProcessingQueue(IEntityStorageEngine storageEngine)
            : this(storageEngine, Array.Empty<ICommitAttemptProcessorRegistration>()) { }

        public CommitAttemptProcessingQueue(
            IEntityStorageEngine storageEngine,
            IReadOnlyList<ICommitAttemptProcessorRegistration> processorRegistrations)
        {
            if (storageEngine is null)
                throw new ArgumentNullException(nameof(storageEngine));

            if (processorRegistrations is null)
                throw new ArgumentNullException(nameof(processorRegistrations));

            _executor = BuildExecutor(storageEngine, processorRegistrations);
        }

        private static ICommitAttemptExecutor BuildExecutor(
            IEntityStorageEngine storageEngine,
            IReadOnlyList<ICommitAttemptProcessorRegistration> processorRegistrations)
        {
            ICommitAttemptExecutor executor = new EntityStorageEngineExecutor(storageEngine);

            for (var i = 0; i < processorRegistrations.Count; i++)
            {
                var processorRegistration = processorRegistrations[i];
                executor = new CommitAttemptProcessorExecutor(processorRegistration, executor);
            }

            return executor;
        }

        public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
            CommitAttempt<TCommitAttemptEntry> commitAttempt,
            IServiceProvider serviceProvider,
            CancellationToken cancellation)
            where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
        {
            return _executor.ProcessCommitAttemptAsync(commitAttempt, serviceProvider, cancellation);
        }

        private sealed class EntityStorageEngineExecutor : ICommitAttemptExecutor
        {
            private readonly IEntityStorageEngine _storageEngine;

            public EntityStorageEngineExecutor(IEntityStorageEngine entityStorageEngine)
            {
                _storageEngine = entityStorageEngine;
            }

            public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
                CommitAttempt<TCommitAttemptEntry> commitAttempt,
                IServiceProvider serviceProvider,
                CancellationToken cancellation)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                return _storageEngine.CommitAsync(commitAttempt, cancellation);
            }
        }

        private sealed class CommitAttemptProcessorExecutor : ICommitAttemptExecutor
        {
            private readonly ICommitAttemptProcessorRegistration _processorRegistration;
            private readonly ICommitAttemptExecutor _nextProcessing;

            public CommitAttemptProcessorExecutor(
                ICommitAttemptProcessorRegistration processorRegistration,
                ICommitAttemptExecutor nextProcessing)
            {
                _processorRegistration = processorRegistration;
                _nextProcessing = nextProcessing;
            }

            public ValueTask<EntityCommitResult> ProcessCommitAttemptAsync<TCommitAttemptEntry>(
                CommitAttempt<TCommitAttemptEntry> commitAttempt,
                IServiceProvider serviceProvider,
                CancellationToken cancellation)
                where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
            {
                if (serviceProvider is null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                var processor = _processorRegistration.CreateCommitAttemptProcessor(serviceProvider);
                var nextProcessingStep = new CommitAttemptProcessingStep(
                    _nextProcessing, serviceProvider, cancellation);

                return processor.ProcessCommitAttemptAsync(commitAttempt, nextProcessingStep, cancellation);
            }
        }
    }
}
