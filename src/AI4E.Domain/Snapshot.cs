/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        Reference.cs 
 * Types:           (1) AI4E.Domain.Reference'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   18.10.2017 
 * --------------------------------------------------------------------------------------------------------------------
 */

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
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Nito.AsyncEx;

namespace AI4E.Domain
{
    /// <summary>
    /// References a snapshot of an aggregate. 
    /// </summary>
    /// <typeparam name="T">The type of aggregate root.</typeparam>
    public readonly struct Snapshot<T> : IEquatable<Snapshot<T>>
        where T : AggregateRoot
    {
        private static readonly int _typeHashCode = typeof(T).GetHashCode();

        private readonly Lazy<ValueTask<T>> _aggregate;

        public Snapshot(T aggregate)
        {
            if (aggregate == null)
            {
                Id = default;
                Revision = default;
            }
            else
            {
                if (aggregate.Id == default)
                {
                    throw new ArgumentException("Cannot get a reference to an aggregate without an id specified.");
                }

                Id = aggregate.Id;
                Revision = aggregate.Revision;
            }

            _aggregate = new Lazy<ValueTask<T>>(() => new ValueTask<T>(aggregate), isThreadSafe: true);
        }

        [MethodImpl(MethodImplOptions.PreserveSig)]
        private Snapshot(Guid id, long revision, IReferenceResolver referenceResolver)
        {
            if (referenceResolver == null)
                throw new ArgumentNullException(nameof(referenceResolver));

            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            Id = id;
            Revision = revision;

            if (id != default)
            {
                _aggregate = new Lazy<ValueTask<T>>(() => referenceResolver.ResolveAsync<T>(id, revision, cancellation: default), isThreadSafe: true);
            }
            else
            {
                _aggregate = new Lazy<ValueTask<T>>(() => new ValueTask<T>(default(T)), isThreadSafe: true);
            }
        }

        public Guid Id { get; }

        public long Revision { get; }

        /// <summary>
        /// Asynchronously resolves the reference and provides an instance of the referenced aggregate.
        /// </summary>
        /// <returns>A task representing the asnychronous operation.</returns>
        public ValueTask<T> ResolveAsync()
        {
            return _aggregate.Value;
        }

        #region Equality

        public bool Equals(Snapshot<T> other)
        {
            return other.Id == Id && other.Revision == Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is Snapshot<T> snapshot && Equals(snapshot);
        }

        public static bool operator ==(Snapshot<T> left, Snapshot<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Snapshot<T> left, Snapshot<T> right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override int GetHashCode()
        {
            return _typeHashCode ^ Id.GetHashCode() ^ Revision.GetHashCode();
        }

        public override string ToString()
        {
            return $"{typeof(T).FullName} #{Id} {Revision}";
        }

        public static implicit operator Snapshot<T>(T aggregate)
        {
            return new Snapshot<T>(aggregate);
        }

        public static Snapshot<T> UnsafeCreate(Guid id, long revision, IReferenceResolver referenceResolver)
        {
            return new Snapshot<T>(id, revision, referenceResolver);
        }
    }

    public static class SnapshotExtension
    {
        public static ValueTaskAwaiter<T> GetAwaiter<T>(in this Snapshot<T> snapshot)
            where T : AggregateRoot
        {
            return snapshot.ResolveAsync().GetAwaiter();
        }

        public static async Task<IEnumerable<T>> ResolveAsync<T>(this IEnumerable<Snapshot<T>> snapshots)
             where T : AggregateRoot
        {
            return await Task.WhenAll(snapshots.Select(p => p.ResolveAsync().AsTask()));
        }

        public static TaskAwaiter<IEnumerable<T>> GetAwaiter<T>(this IEnumerable<Snapshot<T>> snapshots)
            where T : AggregateRoot
        {
            return snapshots.ResolveAsync().GetAwaiter();
        }
    }
}
