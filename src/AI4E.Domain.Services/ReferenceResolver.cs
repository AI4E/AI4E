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
using System.Threading;
using System.Threading.Tasks;
using AI4E.Storage;

namespace AI4E.Domain.Services
{
    public sealed class ReferenceResolver : IReferenceResolver
    {
        private readonly IEntityStore<Guid, DomainEvent, AggregateRoot> _entityStore;

        public ReferenceResolver(IEntityStore<Guid, DomainEvent, AggregateRoot> entityStore)
        {
            if (entityStore == null)
                throw new ArgumentNullException(nameof(entityStore));

            _entityStore = entityStore;
        }

        public Task<TEntity> ResolveAsync<TEntity>(Guid id, long revision, CancellationToken cancellation)
            where TEntity : AggregateRoot
        {
            if (id.Equals(default))
                return Task.FromResult(default(TEntity));

            return _entityStore.GetByIdAsync<TEntity>(id, revision, cancellation);
        }
    }
}
