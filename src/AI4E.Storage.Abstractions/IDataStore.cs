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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Storage
{
    public interface IDataStore
    {
        Task Clear(CancellationToken cancellation = default);

        /// <summary>
        /// Stores an object in the store.
        /// </summary>
        /// <typeparam name="TData">The type of data.</typeparam>
        /// <param name="data">The object to update.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        Task StoreAsync<TData>(TData data, CancellationToken cancellation = default);

        /// <summary>
        /// Removes an object from the store.
        /// </summary>
        /// <typeparam name="TData">The type of data.</typeparam>
        /// <param name="data">The object to remove.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="data"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        Task RemoveAsync<TData>(TData data, CancellationToken cancellation = default);

        /// <summary>
        /// Asynchronously performs a query.
        /// </summary>
        /// <typeparam name="TData">The type of source query.</typeparam>
        /// <typeparam name="TResult">The type of result.</typeparam>
        /// <param name="queryShaper">The query shaper.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// An async enumerable representing the asynchronous operation.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="queryShaper"/> is null.</exception>
        /// <exception cref="ObjectDisposedException">Thrown if the object is disposed.</exception>
        Task<IEnumerable<TResult>> QueryAsync<TData, TResult>(Func<IQueryable<TData>, IQueryable<TResult>> queryShaper, CancellationToken cancellation = default);
    }
}
