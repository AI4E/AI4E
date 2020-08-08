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
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Test.Dummies
{
    internal class DatabaseDummy : IDatabase
    {
        public ValueTask<bool> AddAsync<TEntry>(
            TEntry entry, 
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> UpdateAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public ValueTask<bool> RemoveAsync<TEntry>(
            TEntry entry,
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public ValueTask Clear<TEntry>(
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public IAsyncEnumerable<TEntry> GetAsync<TEntry>(
            Expression<Func<TEntry, bool>> predicate,
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }

        public IDatabaseScope CreateScope()
        {
            throw new NotSupportedException();
        }

        public bool SupportsScopes => throw new NotSupportedException();

        public IAsyncEnumerable<TResult> QueryAsync<TEntry, TResult>(
            Func<IQueryable<TEntry>, IQueryable<TResult>> queryShaper, 
            CancellationToken cancellation = default) where TEntry : class
        {
            throw new NotSupportedException();
        }
    }
}
