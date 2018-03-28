/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IAsyncInitialization.cs 
 * Types:           AI4E.Async.IAsyncInitialization
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.03.2018 
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

namespace AI4E.Async
{
    /// <summary>
    /// Represents a type that requires asynchronous disposal.
    /// </summary>
    public interface IAsyncDisposable : IDisposable
    {
        /// <summary>
        /// Starts the asynchronous disposal. Get <see cref="Disposal"/> to get notified of the disposal state.
        /// </summary>
        new void Dispose();

        /// <summary>
        /// Asynchronously disposes of the current instance. 
        /// This is functionally equivalent with calling <see cref="Dispose"/> and retrieving <see cref="Disposal"/>.
        /// </summary>
        /// <returns>A task representing the asynchronous disposal of the instance.</returns>
        Task DisposeAsync();

        /// <summary>
        /// Gets a task representing the asynchronous disposal of the instance.
        /// </summary>
        Task Disposal { get; }
    }
}
