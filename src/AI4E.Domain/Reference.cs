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

using Nito.AsyncEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;

namespace AI4E.Domain
{
    public readonly struct Reference<T> : IEquatable<Reference<T>>
        where T : AggregateRoot
    {
        private static readonly int _typeHashCode = typeof(T).GetHashCode();

        private readonly AsyncLazy<T> _aggregate;

        public Reference(T aggregate)
        {
            if (aggregate == null)
            {
                Id = Guid.Empty;
            }
            else
            {
                if (aggregate.Id == Guid.Empty)
                {
                    throw new ArgumentException("Cannot get a reference to an aggregate without an id specified.");
                }

                Id = aggregate.Id;
            }

            _aggregate = new AsyncLazy<T>(() => Task.FromResult(aggregate));
        }

        [MethodImpl(MethodImplOptions.PreserveSig)]
        private Reference(Guid id, IReferenceResolver referenceResolver)
        {
            if (referenceResolver == null)
                throw new ArgumentNullException(nameof(referenceResolver));

            Id = id;

            if (id != Guid.Empty)
            {
                _aggregate = new AsyncLazy<T>(async () => await referenceResolver.ResolveAsync<T>(id));
            }
            else
            {
                _aggregate = new AsyncLazy<T>(() => Task.FromResult<T>(null));
            }
        }

        public Guid Id { get; }

        public Task<T> ResolveAsync()
        {
            return _aggregate.Task;
        }

        #region Equality

        public bool Equals(Reference<T> other)
        {
            return other.Id == Id;
        }

        public override bool Equals(object obj)
        {
            return obj is Reference<T> reference && Equals(reference);
        }

        public static bool operator ==(Reference<T> left, Reference<T> right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Reference<T> left, Reference<T> right)
        {
            return !left.Equals(right);
        }

        #endregion

        public override int GetHashCode()
        {
            return _typeHashCode ^ Id.GetHashCode();
        }

        public override string ToString()
        {
            return $"{typeof(T).FullName} #{Id}";
        }

        public static implicit operator Reference<T>(T aggregate)
        {
            return new Reference<T>(aggregate);
        }
    }

    public static class ReferenceExtension
    {
        public static TaskAwaiter<T> GetAwaiter<T>(in this Reference<T> reference)
            where T : AggregateRoot
        {
            return reference.ResolveAsync().GetAwaiter();
        }

        public static async Task<IEnumerable<T>> ResolveAsync<T>(this IEnumerable<Reference<T>> references)
             where T : AggregateRoot
        {
            return await Task.WhenAll(references.Select(p => p.ResolveAsync()));
        }

        public static TaskAwaiter<IEnumerable<T>> GetAwaiter<T>(this IEnumerable<Reference<T>> references)
            where T : AggregateRoot
        {
            return references.ResolveAsync().GetAwaiter();
        }
    }
}
