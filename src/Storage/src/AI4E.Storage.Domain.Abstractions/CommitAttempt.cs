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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a commit-attempt.
    /// </summary>
    /// <typeparam name="TCommitAttemptEntry">The type of commit-attempt entry.</typeparam>
    public readonly struct CommitAttempt<TCommitAttemptEntry> : IEquatable<CommitAttempt<TCommitAttemptEntry>>
         where TCommitAttemptEntry : ICommitAttemptEntry, IEquatable<TCommitAttemptEntry>
    {
        /// <summary>
        /// Creates a new instance of type <see cref="CommitAttempt{TCommitAttemptEntry}"/>.
        /// </summary>
        /// <param name="entries">
        /// The <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> that defines the commit entries that 
        /// the commit-attempt contains of.
        /// </param>
        public CommitAttempt(CommitAttemptEntryCollection<TCommitAttemptEntry> entries)
        {
            Entries = entries;
        }

        /// <summary>
        /// Gets the <see cref="CommitAttemptEntryCollection{TCommitAttemptEntry}"/> that defines the commit entries 
        /// that the current commit-attempt contains of.
        /// </summary>
        public CommitAttemptEntryCollection<TCommitAttemptEntry> Entries { get; }

        bool IEquatable<CommitAttempt<TCommitAttemptEntry>>.Equals(CommitAttempt<TCommitAttemptEntry> other)
        {
            return Equals(in other);
        }

        /// <inheritdoc cref="IEquatable{T}.Equals(T)"/>
        public bool Equals(in CommitAttempt<TCommitAttemptEntry> other)
        {
            return Entries == other.Entries;
        }

        /// <inheritdoc />
        public override bool Equals(object? obj)
        {
            return obj is CommitAttempt<TCommitAttemptEntry> commitAttempt && Equals(in commitAttempt);
        }

        /// <inheritdoc />
        public override int GetHashCode()
        {
            return Entries.GetHashCode();
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempts are equal.
        /// </summary>
        /// <param name="left">The first <typeparamref name="TCommitAttemptEntry"/>.</param>
        /// <param name="right">The second <typeparamref name="TCommitAttemptEntry"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(
            in CommitAttempt<TCommitAttemptEntry> left, 
            in CommitAttempt<TCommitAttemptEntry> right)
        {
            return left.Equals(in right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether two commit-attempts are not equal.
        /// </summary>
        /// <param name="left">The first <typeparamref name="TCommitAttemptEntry"/>.</param>
        /// <param name="right">The second <typeparamref name="TCommitAttemptEntry"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(
            in CommitAttempt<TCommitAttemptEntry> left, 
            in CommitAttempt<TCommitAttemptEntry> right)
        {
            return !left.Equals(in right);
        }
    }

    /// <summary>
    /// Defines constants for possible commit operations.
    /// </summary>
    public enum CommitOperation
    {
        /// <summary>
        /// An entity is not modified. Only domain-events shall be appended.
        /// </summary>
        AppendEventsOnly = 0,

        /// <summary>
        /// An entity shall be created or updated.
        /// </summary>
        Store,

        /// <summary>
        /// An entity shall be deleted.
        /// </summary>
        Delete
    }
}
