/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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

namespace AI4E.Utils.Proxying
{
    /// <summary>
    /// Represents a proxy host.
    /// </summary>
    public interface IProxyHost : IDisposable
    {
        /// <summary>
        /// Asynchronously creates a new instance of the specified type on the remote end-point and returns a proxy for it.
        /// </summary>
        /// <typeparam name="TRemote">The type of instance to create.</typeparam>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the proxy for the remote object.
        /// </returns>
        Task<IProxy<TRemote>> CreateAsync<TRemote>(CancellationToken cancellation)
            where TRemote : class;

        /// <summary>
        /// Asynchronously creates a new instance of the specified type on the remote end-point and returns a proxy for it.
        /// </summary>
        /// <typeparam name="TRemote">The type of instance to create.</typeparam>
        /// <param name="parameter">An array of objects that are used to construct the remote object.</param>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the proxy for the remote object.
        /// </returns>
        Task<IProxy<TRemote>> CreateAsync<TRemote>(object[] parameter, CancellationToken cancellation)
            where TRemote : class;

        /// <summary>
        /// Asynchronously loads an instace of the specified type from the remoted service provider and returns a proxy for it.
        /// </summary>
        /// <typeparam name="TRemote">The type of instance to load.</typeparam>
        /// <param name="cancellation">
        /// A <see cref="CancellationToken"/> used to cancel the asynchronous operation, or <see cref="CancellationToken.None"/>.
        /// </param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the proxy for the remote object.
        /// </returns>
        Task<IProxy<TRemote>> LoadAsync<TRemote>(CancellationToken cancellation)
            where TRemote : class;
    }
}
