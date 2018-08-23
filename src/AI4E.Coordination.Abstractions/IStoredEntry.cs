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

namespace AI4E.Coordination
{
    public interface IStoredEntry
    {
        /// <summary>
        /// Gets the path of the entry.
        /// </summary>
        string Path { get; }

        /// <summary>
        /// Gets the value of the entry.
        /// </summary>
        ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Gets a collection of session that read locks are held for.
        /// </summary>
        ImmutableArray<string> ReadLocks { get; }

        /// <summary>
        /// Gets the session that a write lock is held for.
        /// </summary>
        string WriteLock { get; }

        /// <summary>
        /// Gets the version of the entry.
        /// </summary>
        int Version { get; }

        int StorageVersion { get; }

        /// <summary>
        /// Gets the date and time the entry was created.
        /// </summary>
        DateTime CreationTime { get; }

        /// <summary>
        /// Gets the date and time the entries value was written to last.
        /// </summary>
        DateTime LastWriteTime { get; }

        /// <summary>
        /// Get a collection of names that represents the child entries of the entry.
        /// </summary>
        ImmutableArray<string> Childs { get; }

        string EphemeralOwner { get; }
    }
}
