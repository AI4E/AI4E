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
using System.Diagnostics.CodeAnalysis;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a scopeable entity query-result.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of scopeable entity query-result.</typeparam>
    /// <remarks>
    /// A scope is defined by an instance of type <see cref="IEntityStorage"/>.
    /// Scoping is done, due to entity load-results have to be immutable and thus thread-safe so they can safely
    /// by used by multiple entity storages (for example when using a cached version). If an entity load-result contains
    /// an object or a value that immutability (or thread-safety) cannot be guaranteed, the entity load-result has
    /// to be defined as scopeable. 
    /// An entity-storage engine guarantees to scope a scopeable entity load-result before use to its the scope it
    /// defines. The scopeable entity load-result can perform actions to guarantee thread-safety for this scope
    /// accordingly, for example by copying the object or value that is not thread-safe.
    /// </remarks>
    public interface IScopeableEntityQueryResult<out TQueryResult> : IEntityQueryResult
        where TQueryResult : class, IEntityQueryResult
    {
        /// <summary>
        /// Returns an instance of <see cref="IScopedEntityQueryResult{TQueryResult}"/> representing the current 
        /// scope-able entity query-result scoped to the specified <see cref="IEntityQueryResultScope"/>.
        /// </summary>
        /// <param name="scope">The <see cref="IEntityQueryResultScope"/> that defines the scope.</param>
        /// <returns>
        /// A <see cref="IScopedEntityQueryResult{TQueryResult}"/> that is scoped to <paramref name="scope"/>.
        /// </returns>
        /// <exception cref="ArgumentNullException">Thrown of <paramref name="scope"/> is <c>null</c>.</exception>
        IScopedEntityQueryResult<TQueryResult> AsScopedTo(IEntityQueryResultScope scope);
    }

    /// <summary>
    /// Represents a scoped entity query-result.
    /// </summary>
    /// <typeparam name="TQueryResult">The type of scopeable entity query-result.</typeparam>
    public interface IScopedEntityQueryResult<out TQueryResult> : IScopeableEntityQueryResult<TQueryResult>
        where TQueryResult : class, IEntityQueryResult
    {
        /// <summary>
        /// Gets the <see cref="IEntityQueryResultScope"/> the current instance is scoped to.
        /// </summary>
        IEntityQueryResultScope Scope { get; }

        /// <summary>
        /// Returns a scoped version of the scopeable entity query result.
        /// </summary>
        TQueryResult ToQueryResult();
    }

    /// <summary>
    /// Represents the scope of an entity query-result.
    /// </summary>
    public interface IEntityQueryResultScope
    {
        /// <summary>
        /// Scopes the specified entity to the current scope.
        /// </summary>
        /// <param name="originalEntity">The entity to scope.</param>
        /// <returns>The scoped entity.</returns>
        object ScopeEntity(object originalEntity);
    }
}