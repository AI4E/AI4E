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
        ImmutableArray<byte> Value { get; }

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

        /// <summary>
        /// Acquires a write lock for the specified session and returns an entry with the lock aquired.
        /// </summary>
        /// <param name="session">The session that requests the lock.</param>
        /// <returns>An entry with the lock aquired.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a write lock is present for another session.</exception>
        IStoredEntry AcquireWriteLock(string session);

        /// <summary>
        /// Releases a write lock and returns an entry with the lock released.
        /// </summary>
        /// <returns>An entry with the lock released.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a read lock is present for an arbitrary session.</exception>
        IStoredEntry ReleaseWriteLock();

        /// <summary>
        /// Acquires a read lock for the specified session and returns an entry with the lock aquired.
        /// </summary>
        /// <param name="session">The session that requests the lock.</param>
        /// <returns>An entry with the lock aquired.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
        /// <exception cref="InvalidOperationException">Thrown if a write lock is present for an arbitrary session.</exception>
        /// <remarks>
        /// If the read lock is already taken by the specified session, the lock is not taken twice but only once.
        /// Even if taken multiple times, the lock is released with a single call.
        /// </remarks>
        IStoredEntry AcquireReadLock(string session);

        /// <summary>
        /// Releases a read lock and returns an entry with the lock released.
        /// </summary>
        /// <param name="session">The session that the read lock is held for.</param>
        /// <returns>An entry with the lock released.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="session"/> is null.</exception>
        IStoredEntry ReleaseReadLock(string session);

        /// <summary>
        /// Removes the entry and returns an entry that represents the removal of the entry.
        /// </summary>
        /// <returns>An entry that represents the removal of the entry or null.</returns>
        /// <exception cref="InvalidOperationException">
        /// Thrown if either a read lock is held by an arbitrary session or 
        /// no write lock is held or
        /// the entry contains childs.
        /// </exception>
        /// <remarks>
        /// The return value may be null. This is implementation dependent.
        /// </remarks>
        IStoredEntry Remove();

        /// <summary>
        /// Sets the value of the entry and increment its version counter.
        /// </summary>
        /// <param name="value">The value to set.</param>
        /// <returns>An entry with the specified value set.</returns>
        /// <exception cref="InvalidOperationException">Thrown if a read lock is held by an arbitrary session or no write lock is held.</exception>
        IStoredEntry SetValue(ImmutableArray<byte> value);

        /// <summary>
        /// Adds a child to the entry and returns an entry with the child added.
        /// </summary>
        /// <param name="name">The name of the child entry.</param>
        /// <returns>An entry with the child added.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
        IStoredEntry AddChild(string name);

        /// <summary>
        /// Removes a child from the entry and returns an entry with the child removed.
        /// </summary>
        /// <param name="name">The name of the child entry.</param>
        /// <returns>An entry with the child removed.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="name"/> is null.</exception>
        IStoredEntry RemoveChild(string name);
    }
}
