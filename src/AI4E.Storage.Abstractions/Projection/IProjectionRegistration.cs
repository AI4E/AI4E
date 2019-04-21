/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        IProjectionRegistration.cs 
 * Types:           AI4E.Storage.Projection.IProjectionRegistration
 *                  AI4E.Storage.Projection.IProjectionRegistration'1
 * Version:         1.0
 * Author:          Andreas Trütschel
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

using AI4E.Utils.Async;

#if SUPPORTS_ASYNC_ENUMERABLE
using System;
#endif

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents a cancellable handler registration.
    /// </summary> 
    public interface IProjectionRegistration : IAsyncDisposable, IAsyncInitialization
    {
    }

    /// <summary>
    /// Represents a cancellable handler registration of the specified type of handler.
    /// </summary>
    /// <typeparam name="TProjection">The type of projection.</typeparam>
    public interface IProjectionRegistration<TProjection> : IProjectionRegistration
    {
        /// <summary>
        /// Gets a contextual provider that provides instances of the registered handler.
        /// </summary>
        IContextualProvider<TProjection> Projection { get; }
    }
}
