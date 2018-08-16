/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        HandlerRegistration.cs 
 * Types:           AI4E.HandlerRegistration
 *                  AI4E.HandlerRegistrationSource
 *                  AI4E.HandlerRegistrationSource'1
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
using System.Threading.Tasks;
using AI4E.Async;

namespace AI4E
{
    /// <summary>
    /// Represents a cancellable handler registration.
    /// </summary>
    [Obsolete("Use AI4E.Async.IAsyncDisposable")]
    public interface IHandlerRegistration
    {
        /// <summary>
        /// Gets a task that represents the asynchronous cancellation of the registration.
        /// </summary>
        Task Cancellation { get; }

        /// <summary>
        /// Cancels the registration.
        /// </summary>
        void Cancel();
    }

#pragma warning disable CS0618
    /// <summary>
    /// Represents a cancellable handler registration of the specified type of handler.
    /// </summary>
    /// <typeparam name="THandler">The type of handler.</typeparam>
    public interface IHandlerRegistration<THandler> : IHandlerRegistration, IAsyncDisposable, IAsyncInitialization
#pragma warning restore CS0618 
    {
        /// <summary>
        /// Gets a contextual provider that provides instances of the registered handler.
        /// </summary>
        IContextualProvider<THandler> Handler { get; }
    }
}
