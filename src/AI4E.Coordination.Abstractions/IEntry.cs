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
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    /// <summary>
    /// Represents an entry of the coordination service.
    /// </summary>
    public interface IEntry
    {
        /// <summary>
        /// Gets the entry's name.
        /// </summary>
        CoordinationEntryPathSegment Name { get; }

        /// <summary>
        /// Gets the entry's path.
        /// </summary>
        CoordinationEntryPath Path { get; }

        /// <summary>
        /// Gets the entry's version.
        /// </summary>
        int Version { get; }

        /// <summary>
        /// Gets the timestamp when the entry was created.
        /// </summary>
        DateTime CreationTime { get; }

        /// <summary>
        /// Gets the timestamp when the entry was last written to.
        /// </summary>
        DateTime LastWriteTime { get; }

        /// <summary>
        /// Gets the value of the entry.
        /// </summary>
        ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Gets the path of the entry's parent entry.
        /// </summary>
        CoordinationEntryPath ParentPath { get; }

        /// <summary>
        /// Gets a collection of entry's names that are children of the entry.
        /// </summary>
        IReadOnlyList<CoordinationEntryPathSegment> Children { get; }

        /// <summary>
        /// Gets the coordination manager that this entry is managed by.
        /// </summary>
        ICoordinationManager CoordinationManager { get; }
    }

    public static class EntryExtension
    {
        public static IAsyncEnumerable<IEntry> GetChildrenEntries(this IEntry entry)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return new ChildrenEnumerable(entry);
        }

        public static async ValueTask<IEnumerable<IEntry>> GetChildrenEntriesAsync(this IEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var result = new List<IEntry>(capacity: entry.Children.Count);

            for (var i = 0; i < entry.Children.Count; i++)
            {
                var child = entry.Children[i];
                var childFullName = entry.Path.GetChildPath(child);

                var childEntry = await entry.CoordinationManager.GetAsync(childFullName, cancellation);

                if (childEntry != null)
                {
                    result.Add(childEntry);
                }
            }

            return result;
        }

        public static ValueTask<IEntry> GetParentAsync(this IEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            return entry.CoordinationManager.GetAsync(entry.ParentPath, cancellation);
        }

        public static Stream OpenStream(this IEntry entry)
        {
            return new ReadOnlyStream(entry.Value);
        }

        private sealed class ChildrenEnumerable : IAsyncEnumerable<IEntry>
        {
            private readonly IEntry _entry;

            public ChildrenEnumerable(IEntry entry)
            {
                Assert(entry != null);

                _entry = entry;
            }

            public IAsyncEnumerator<IEntry> GetEnumerator()
            {
                return new ChildrenEnumerator(_entry);
            }
        }

        private sealed class ChildrenEnumerator : IAsyncEnumerator<IEntry>
        {
            private readonly IEntry _entry;
            private int _currentIndex = -1;

            public ChildrenEnumerator(IEntry entry)
            {
                Assert(entry != null);

                _entry = entry;
            }

            public async Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                IEntry next;

                do
                {
                    CoordinationEntryPathSegment child;
                    var index = ++_currentIndex;

                    if (index >= _entry.Children.Count)
                    {
                        Current = default;
                        return false;
                    }

                    child = _entry.Children[index];
                    var childFullName = _entry.Path.GetChildPath(child);
                    next = await _entry.CoordinationManager.GetAsync(childFullName, cancellationToken);
                }
                while (next == null);

                Current = next;
                return true;
            }

            public IEntry Current { get; private set; } = default;

            public void Dispose() { }
        }
    }
}
