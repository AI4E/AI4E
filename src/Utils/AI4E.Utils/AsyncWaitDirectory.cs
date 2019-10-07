/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Diagnostics;

namespace AI4E.Utils
{
    public sealed class AsyncWaitDirectory<TKey>
        where TKey : notnull
    {
        private readonly Dictionary<TKey, (TaskCompletionSource<object?> tcs, int refCount)> _entries;

        public AsyncWaitDirectory()
        {
            _entries = new Dictionary<TKey, (TaskCompletionSource<object?> tcs, int refCount)>();
        }

        public async Task WaitForNotificationAsync(TKey key, CancellationToken cancellation)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            cancellation.ThrowIfCancellationRequested();

            var waitEntry = AllocateWaitEntry(key);

            using (cancellation.Register(() => FreeWaitEntry(key)))
            {
                await waitEntry.WithCancellation(cancellation).ConfigureAwait(false);
            }
        }

        public void Notify(TKey key)
        {
            if (key == null)
                throw new ArgumentNullException(nameof(key));

            TaskCompletionSource<object?> tcs;

            lock (_entries)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    return;
                }

                tcs = entry.tcs;
                _entries.Remove(key);
            }

            tcs.SetResult(null);
        }

        private Task AllocateWaitEntry(TKey key)
        {
            TaskCompletionSource<object?> tcs;

            lock (_entries)
            {
                var refCount = 0;

                if (_entries.TryGetValue(key, out var entry))
                {
                    tcs = entry.tcs;
                    refCount = entry.refCount;
                }
                else
                {
                    tcs = new TaskCompletionSource<object?>();
                }

                _entries[key] = (tcs, refCount + 1);
            }

            return tcs.Task;
        }

        private void FreeWaitEntry(TKey key)
        {
            lock (_entries)
            {
                if (!_entries.TryGetValue(key, out var entry))
                {
                    return;
                }

                Debug.Assert(entry.refCount >= 1);

                if (entry.refCount == 1)
                {
                    _entries.Remove(key);
                }
                else
                {
                    _entries[key] = (entry.tcs, entry.refCount - 1);
                }
            }
        }
    }
}
