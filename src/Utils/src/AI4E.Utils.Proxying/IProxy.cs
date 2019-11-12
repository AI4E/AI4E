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
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Utils.Proxying
{
    /// <summary>
    /// Represents a proxy.
    /// </summary>
    public interface IProxy : IAsyncDisposable
    {
        Task Disposal { get; }

        /// <summary>
        /// Gets the local proxied instance or null if this is a remote proxy.
        /// </summary>
        object? LocalInstance { get; }

        /// <summary>
        /// Gets the static type of the proxy.
        /// </summary>
        Type RemoteType { get; }

        /// <summary>
        /// Asynchronously returns the dynamic type of the proxied instance.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A <see cref="ValueTask{Type}"/> that represents the asynchronous operation.
        /// When evaluated, the tasks result contains the dynamic type of the proxied instance.
        /// </returns>
        ValueTask<Type> GetObjectTypeAsync(CancellationToken cancellation = default);

        /// <summary>
        /// Casts the proxy to a proxy with the specified remote type.
        /// </summary>
        /// <typeparam name="TCast">The remote type of the result proxy.</typeparam>
        /// <returns>The cast proxy.</returns>
        /// <exception cref="ArgumentException">Thrown if the object type is not assignable to <typeparamref name="TCast"/>.</exception>
        IProxy<TCast> Cast<TCast>() where TCast : class;
    }

    /// <summary>
    /// Represents a proxy of the specified type.
    /// </summary>
    /// <typeparam name="TRemote">The static type of the proxied object.</typeparam>
    public interface IProxy<TRemote> : IProxy
        where TRemote : class
    {
        /// <summary>
        /// Gets the local proxied instance or null if this is a remote proxy.
        /// </summary>
        new TRemote? LocalInstance { get; }

        /// <summary>
        /// Asynchronously invokes a member on the proxy instance.
        /// </summary>
        /// <param name="expression">The expression that described the invokation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ExecuteAsync(Expression<Action<TRemote>> expression);

        /// <summary>
        /// Asynchronously invokes a member on the proxy instance.
        /// </summary>
        /// <param name="expression">The expression that described the invokation.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation.</returns>
        Task ExecuteAsync(Expression<Func<TRemote, Task>> expression);

        /// <summary>
        /// Asynchronously invokes a member on the proxy instance.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="expression">The expression that described the invokation.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the result of the invokation.
        /// </returns>
        Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, TResult>> expression);

        /// <summary>
        /// Asynchronously invokes a member on the proxy instance.
        /// </summary>
        /// <typeparam name="TResult">The result type.</typeparam>
        /// <param name="expression">The expression that described the invokation.</param>
        /// <returns>
        /// A <see cref="Task"/> representing the asynchronous operation.
        /// When evaluated, the tasks result contains the result of the invokation.
        /// </returns>
        Task<TResult> ExecuteAsync<TResult>(Expression<Func<TRemote, Task<TResult>>> expression);

        /// <summary>
        /// Returns a transparent proxy for the proxy.
        /// </summary>
        /// <returns>The transparent proxy.</returns>
        /// <exception cref="NotSupportedException">Thrown if <typeparamref name="TRemote"/> is not an interface.</exception>
        TRemote AsTransparentProxy();
    }
}
