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
using Nito.AsyncEx;

namespace AI4E.Processing
{
    internal sealed class AsyncProcessScheduler
    {
        private volatile ImmutableHashSet<ITrigger> _triggers = ImmutableHashSet<ITrigger>.Empty;
        private readonly AsyncManualResetEvent _event = new AsyncManualResetEvent();

        private CancellationTokenSource _cancelSource = null;
        private bool _triggersTouched = false;
        private readonly object _lock = new object();

        public AsyncProcessScheduler() { }

        public void Trigger()
        {
            _event.Set();
        }

        public Task NextTrigger()
        {
            return InternalNextTrigger();
        }

        private async Task InternalNextTrigger()
        {
            bool needsRerun;

            do
            {
                // Reload all triggers
                var triggers = _triggers; // Volatile read op.

                needsRerun = false;

                CancellationTokenSource cancellationSource;

                lock (_lock)
                {
                    if (_triggersTouched)
                    {
                        _triggersTouched = false;
                        needsRerun = true;
                        continue;
                    }

                    _cancelSource = new CancellationTokenSource();
                    cancellationSource = _cancelSource;
                }

                // Set a new cancellation source
                using (cancellationSource)
                {
                    // Get a list of all triggers
                    var awaitedTasks = triggers.Select(p => p.NextTriggerAsync(_cancelSource.Token)).Append(_event.WaitAsync(_cancelSource.Token));

                    // Asynchronously wait for any tasks to complete.
                    var completedTask = await Task.WhenAny(awaitedTasks);

                    try
                    {
                        // Trigger any thrown exceptions.
                        await completedTask;
                    }
                    catch (OperationCanceledException) when (_cancelSource.IsCancellationRequested)
                    {
                        needsRerun = true;
                    }

                    // Cancel all trigger tasks that were not completed.
                    _cancelSource.Cancel();

                    lock (_lock)
                    {
                        _cancelSource = null;
                    }
                }
            }
            while (needsRerun);

            _event.Reset();
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
                lock (_lock)
                {
                    if (_cancelSource == null)
                    {
                        _triggersTouched = true;
                    }
                    else
                    {
                        _cancelSource.Cancel();
                    }
                }
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
                lock (_lock)
                {
                    if (_cancelSource == null)
                    {
                        _triggersTouched = true;
                    }
                    else
                    {
                        _cancelSource.Cancel();
                    }
                }
            }
        }
    }
}
