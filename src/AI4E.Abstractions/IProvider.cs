/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IProvider.cs
 * Types:           (1) AI4E.IProvider'1
 *                  (2) AI4E.IContextualProvider'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   25.02.2018 
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
using System.Threading;
using System.Threading.Tasks;

namespace AI4E
{
    /// <summary>
    /// Represents a provider for the specified type.
    /// </summary>
    /// <typeparam name="T">The type that the provider can deliver an instance of.</typeparam>
    public interface IProvider<out T>
    {
        /// <summary>
        /// Provides an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <returns>An object of type <typeparamref name="T"/>.</returns>
        T ProvideInstance();
    }

    /// <summary>
    /// Represents an asynchronous provider for the specified type.
    /// </summary>
    /// <typeparam name="T">The type that the provider can deliver an instance of.</typeparam>
    public interface IAsyncProvider<T>
    {
        /// <summary>
        /// Asynchronously provides an instance of type <typeparamref name="T"/>.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>
        /// A task representing the asynchronous operation.
        /// When evaluated, the tasks result contains an object of type <typeparamref name="T"/>.
        /// </returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        Task<T> ProvideInstanceAsync(CancellationToken cancellation = default);
    }

    /// <summary>
    /// Represents a contextual provider for the specified type.
    /// </summary>
    /// <typeparam name="T">The type that the provider can deliver an instance of.</typeparam>
    public interface IContextualProvider<out T>
    {
        /// <summary>
        /// Provides an instance of type <typeparamref name="T"/> within a context.
        /// </summary>
        /// <param name="serviceProvider">The service provider that can be used to get services from the context.</param>
        /// <returns>An object of type <typeparamref name="T"/>.</returns>
        T ProvideInstance(IServiceProvider serviceProvider);
    }
}
