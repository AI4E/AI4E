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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage.Domain
{
    public interface IEntityStore : IDisposable
    {
        ValueTask<object> GetByIdAsync(Type entityType, string id, CancellationToken cancellation = default);

        ValueTask<object> GetByIdAsync(Type entityType, string id, long revision, CancellationToken cancellation = default);

        IAsyncEnumerable<object> GetAllAsync(Type entityType, CancellationToken cancellation = default);

        IAsyncEnumerable<object> GetAllAsync(CancellationToken cancellation = default);

        Task StoreAsync(Type entityType, object entity, CancellationToken cancellation = default);

        Task DeleteAsync(Type entityType, object entity, CancellationToken cancellation = default);

        //IEnumerable<(Type type, string id, long revision, object entity)> CachedEntries { get; }
    }
}
