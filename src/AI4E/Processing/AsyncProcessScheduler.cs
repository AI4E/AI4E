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

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E.Processing
{
    internal sealed class AsyncProcessScheduler
    {
        private volatile ImmutableHashSet<ITrigger> _triggers = ImmutableHashSet<ITrigger>.Empty;
        private CancellationTokenSource _cancelSource = null;

        public AsyncProcessScheduler() { }

        public Task NextTrigger()
        {
            return InternalNextTrigger();
        }

        private async Task InternalNextTrigger()
        {
            Task completedTask;
            Task cancellationTask;

            ImmutableHashSet<ITrigger> triggers;

            do
            {
                // Reload all triggers
                triggers = _triggers; // Volatile read op.

                // Set a new cancellation source
                _cancelSource = new CancellationTokenSource();

                // Get a list of all trigger tasks plus a cancellation task
                cancellationTask = _cancelSource.Token.AsTask();
                var awaitedTasks = triggers.Select(p => p.NextTrigger(_cancelSource.Token)).Append(cancellationTask);

                // Asynchronously wait for any tasks to complete.
                completedTask = await Task.WhenAny(awaitedTasks);

                // Cancel all trigger tasks that were not completed.
                _cancelSource.Cancel();

                // Trigger any thrown exceptions.
                await completedTask;
            }
            while (completedTask != cancellationTask);
        }

        public void AddTrigger(ITrigger trigger)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));

            ImmutableHashSet<ITrigger> current = _triggers,
                                       start,
                                       desired;

            do
            {
                start = current;
                desired = start.Add(trigger);
                current = Interlocked.CompareExchange(ref _triggers, desired, start);
            }
            while (start != current);

            if (start != desired)
            {
                _cancelSource.Cancel();
            }
        }

        public void RemoveTrigger(ITrigger trigger)
        {
            if (trigger == null)
                throw new ArgumentNullException(nameof(trigger));

            ImmutableHashSet<ITrigger> current = _triggers,
                                       start,
                                       desired;

            do
            {
                start = current;
                desired = start.Remove(trigger);
                current = Interlocked.CompareExchange(ref _triggers, desired, start);
            }
            while (start != current);

            if (start != desired)
            {
                _cancelSource.Cancel();
            }
        }
    }
}
