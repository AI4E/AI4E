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
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    public interface IStoredEntry
    {
        /// <summary>
        /// Gets the path of the entry.
        /// </summary>
        CoordinationEntryPath Path { get; }

        /// <summary>
        /// Gets the value of the entry.
        /// </summary>
        ReadOnlyMemory<byte> Value { get; }

        /// <summary>
        /// Gets a collection of session that read locks are held for.
        /// </summary>
        ImmutableArray<CoordinationSession> ReadLocks { get; }

        /// <summary>
        /// Gets the session that a write lock is held for.
        /// </summary>
        CoordinationSession? WriteLock { get; }

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
        /// Get an ordered collection of names that represents the child entries of the entry.
        /// </summary>
        ImmutableList<CoordinationEntryPathSegment> Children { get; }
        // The child names MUST be ordered (that means, the order of children is the same for all instances of the coordination manager)
        // in order to prevent dead-lock situations in recursive operations.

        CoordinationSession? EphemeralOwner { get; }
    }
}
