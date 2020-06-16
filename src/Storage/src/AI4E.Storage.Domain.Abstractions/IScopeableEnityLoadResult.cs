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

namespace AI4E.Storage.Domain
{
    ///// <summary>
    ///// Represents a scopeable entity load result.
    ///// </summary>
    ///// <remarks>
    ///// A scope is defined by an instance of type <see cref="IEntityStorage"/>.
    ///// Scoping is done, due to entity load-results have to be immutable and thus thread-safe so they can safely
    ///// by used by multiple entity storages (for example when using a cached version). If an entity load-result contains
    ///// an object or a value that immutability (or thread-safety) cannot be guaranteed, the entity load-result has
    ///// to be defined as scopeable. 
    ///// An entity-storage engine guarantees to scope a scopeable entity load-result before use to its the scope it
    ///// defines. The scopeable entity load-result can perform actions to guarantee thread-safety for this scope
    ///// accordingly, for example by copying the object or value that is not thread-safe.
    ///// </remarks>
    //public interface IScopeableEnityLoadResult : IEntityLoadResult
    //{
    //    /// <summary>
    //    /// Gets the <see cref="IEntityStorage"/> the current instance is scoped to, or
    //    /// <c>null</c> if the current instance is not scoped to a particular <see cref="IEntityStorage"/>.
    //    /// </summary>
    //    IEntityStorage? Scope { get; }

    //    /// <summary>
    //    /// Returns a copy of the current instance scoped to the specified <see cref="IEntityStorage"/>.
    //    /// </summary>
    //    /// <param name="entityStorage">The <see cref="IEntityStorage"/> that defines the scope.</param>
    //    /// <returns>
    //    /// A <see cref="IScopeableEnityLoadResult"/> that is scoped to <paramref name="entityStorage"/>.
    //    /// </returns>
    //    /// <exception cref="ArgumentNullException">
    //    /// Thrown if <paramref name="entityStorage"/> is <c>null</c>.
    //    /// </exception>
    //    IScopeableEnityLoadResult ScopeTo(IEntityStorage entityStorage);

    //    /// <summary>
    //    /// Returns an unscoped copy of the current instance.
    //    /// </summary>
    //    /// <returns>A <see cref="IScopeableEnityLoadResult"/> that is unscoped.</returns>
    //    IScopeableEnityLoadResult Unscope();
    //}
}