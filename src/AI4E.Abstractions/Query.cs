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

namespace AI4E
{
    /// <summary>
    /// Represents a query of the specified result without any conditions.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public class Query<TResult> { }

    /// <summary>
    /// Represents a query of the specified result that is identified by id.
    /// </summary>
    /// <typeparam name="TId">The type of id.</typeparam>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public class ByIdQuery<TId, TResult> : Query<TResult>
        where TId : struct, IEquatable<TId>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ByIdQuery{TId, TResult}"/> with the specified id.
        /// </summary>
        /// <param name="id">The id that identifies the result.</param>
        public ByIdQuery(TId id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets the id that identifies the result.
        /// </summary>
        public TId Id { get; }
    }

    /// <summary>
    /// Represents a query of the specified result that is identified by id.
    /// </summary>
    /// <typeparam name="TResult">The type of result.</typeparam>
    public class ByIdQuery<TResult> : Query<TResult>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ByIdQuery{TResult}"/> with the specified id.
        /// </summary>
        /// <param name="id">The id that identifies the result.</param>
        public ByIdQuery(Guid id)
        {
            Id = id;
        }

        /// <summary>
        /// Gets or sets the id that identifies the result.
        /// </summary>
        public Guid Id { get; }
    }

    /// <summary>
    /// Represents a query of the specified result that is identified by its parent.
    /// </summary>
    /// <typeparam name="TId">The type of id.</typeparam>
    /// <typeparam name="TResult">The type of parent.</typeparam>
    public class ByParentQuery<TId, TResult> : Query<TResult>
        where TId : struct, IEquatable<TId>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ByParentQuery{TId, TResult}"/> with the specified parent-id.
        /// </summary>
        /// <param name="parentId">The id that identifies the results parent.</param>
        public ByParentQuery(TId parentId)
        {
            ParentId = parentId;
        }

        /// <summary>
        /// Gets or sets the id that identifies the results parent.
        /// </summary>
        public TId ParentId { get; }
    }

    /// <summary>
    /// Represents a query of the specified result that is identified by its parent.
    /// </summary>
    /// <typeparam name="TResult">The type of parent.</typeparam>
    public class ByParentQuery<TResult> : Query<TResult>
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ByParentQuery{TParent, TResult}"/>.
        /// </summary>
        public ByParentQuery() { }

        /// <summary>
        /// Creates a new instance of the <see cref="ByParentQuery{TParent, TResult}"/> with the specified parent-id.
        /// </summary>
        /// <param name="parentId">The id that identifies the results parent.</param>
        public ByParentQuery(Guid parentId)
        {
            ParentId = parentId;
        }

        /// <summary>
        /// Gets or sets the id that identifies the results parent.
        /// </summary>
        public Guid ParentId { get; }
    }
}
