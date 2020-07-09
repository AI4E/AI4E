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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;

#nullable enable

namespace AI4E.AspNetCore.Components.Notifications.Test.Helpers
{
    public sealed class TestDateTimeProvider : IDateTimeProvider
    {
        private readonly LinkedList<WaitEntry> _waitEntries = new LinkedList<WaitEntry>();
        private DateTime _currentTime;

        public DateTime CurrentTime
        {
            get => _currentTime;
            set
            {
                if (_currentTime == value)
                    return;

                _currentTime = value;

                for (var current = _waitEntries.First; current != null && current.Value.TargetTime >= _currentTime;)
                { 
                    var next = current.Next;
                    current.Value.Complete();
                    if (current.List == _waitEntries)
                    {
                        _waitEntries.Remove(current);
                    }
                    current = next;
                }
            }
        }

        public TestDateTimeProvider(DateTime currentTime)
        {
            _currentTime = currentTime;
        }

        public TestDateTimeProvider() : this(DateTime.UtcNow) { }

        public DateTime GetCurrentTime()
        {
            return CurrentTime;
        }

        public async ValueTask DelayAsync(TimeSpan delay, CancellationToken cancellation = default)
        {
            var currentTime = CurrentTime;
            var targetTime = currentTime + delay;
            var taskCompletionSource = new TaskCompletionSource<byte>();
            var waitEntry = new WaitEntry(targetTime, taskCompletionSource);
            var node = InsertWaitEntry(waitEntry);

            try
            {
                await taskCompletionSource.Task.WithCancellation(cancellation);
            }
            finally
            {
                if (node.List != null)
                {
                    _waitEntries.Remove(node);
                }
            }
        }

        public void CompleteAllDelays()
        {
            foreach (var entry in _waitEntries.ToList())
            {
                entry.Complete();
            }

            _waitEntries.Clear();
        }

        private LinkedListNode<WaitEntry> InsertWaitEntry(WaitEntry waitEntry)
        {
            var current = _waitEntries.First;
            for (; current != null && current.Value.TargetTime <= _currentTime; current = current.Next) ;

            if (current is null)
            {
                return _waitEntries.AddLast(waitEntry);
            }
            else
            {
                return _waitEntries.AddBefore(current, waitEntry);
            }
        }

        private sealed class WaitEntry
        {
            private readonly TaskCompletionSource<byte> _taskCompletionSource;

            public WaitEntry(DateTime targetTime, TaskCompletionSource<byte> taskCompletionSource)
            {
                TargetTime = targetTime;
                _taskCompletionSource = taskCompletionSource;
            }

            public DateTime TargetTime { get; }

            public void Complete()
            {
                _taskCompletionSource.TrySetResult(0);
            }
        }
    }
}
