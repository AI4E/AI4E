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

namespace AI4E.Storage
{
    //public interface IEntityAccessor<TId, TEventBase, TEntityBase>
    //    where TId : struct, IEquatable<TId>
    //    where TEventBase : class
    //    where TEntityBase : class
    //{
    //    /// <summary>
    //    /// Returns the identifier of the specified entity.
    //    /// </summary>
    //    /// <param name="entity">The entity whose identifier is retrived.</param>
    //    /// <returns>The identifier of <paramref name="entity"/>.</returns>
    //    TId GetId(TEntityBase entity);

    //    Guid GetConcurrencyToken(TEntityBase entity);
    //    void SetConcurrencyToken(TEntityBase entity, Guid concurrencyToken);

    //    long GetRevision(TEntityBase entity);
    //    void SetRevision(TEntityBase entity, long revision);

    //    void CommitEvents(TEntityBase entity);

    //    IEnumerable<TEventBase> GetUncommittedEvents(TEntityBase entity);
    //}

    public interface IEntityAccessor
    {
        /// <summary>
        /// Returns the identifier of the specified entity.
        /// </summary>
        /// <param name="entity">The entity whose identifier is retrived.</param>
        /// <returns>The identifier of <paramref name="entity"/>.</returns>
        string GetId(Type entityType, object entity);

        string GetConcurrencyToken(Type entityType, object entity);
        void SetConcurrencyToken(Type entityType, object entity, string concurrencyToken);

        long GetRevision(Type entityType, object entity);
        void SetRevision(Type entityType, object entity, long revision);

        void CommitEvents(Type entityType, object entity);

        IEnumerable<object> GetUncommittedEvents(Type entityType, object entity);
    }
}
