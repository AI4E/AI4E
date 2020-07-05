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
    /// <summary>
    /// Represents a track-able entity load-result.
    /// </summary>
    /// <typeparam name="TLoadResult">The type of track-able load-result.</typeparam>
    /// <remarks>
    /// Track-ability means that the load-result and changes to it can be tracked in the scope of a single user's 
    /// activities.
    /// </remarks>
    public interface ITrackableEntityLoadResult<out TLoadResult> : IEntityLoadResult
        where TLoadResult : class, IEntityLoadResult
    {
        /// <summary>
        /// Returns an instance of <see cref="ITrackedEntityLoadResult{TLoadResult}"/> representing the tracking of the
        /// current track-able entity load-result.
        /// </summary>
        /// <returns>
        /// A <see cref="ITrackedEntityLoadResult{TLoadResult}"/> tracking the current entity load-result.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="concurrencyTokenFactory"/> is <c>null</c>.
        /// </exception>
        ITrackedEntityLoadResult<TLoadResult> AsTracked(IEntityConcurrencyTokenFactory concurrencyTokenFactory);
    }

    /// <summary>
    /// Represents the tracking of a track-able entity load-result.
    /// </summary>
    /// <typeparam name="TLoadResult">The type of track-able load-result.</typeparam>
    public interface ITrackedEntityLoadResult<out TLoadResult> : ITrackableEntityLoadResult<TLoadResult>
        where TLoadResult : class, IEntityLoadResult
    {
        /// <summary>
        /// Gets the underlying tracked entity load-result without operations applied.
        /// </summary>
        TLoadResult TrackedLoadResult { get; }

        /// <summary>
        /// Appends a delete operation to the tracking.
        /// </summary>
        /// <returns>A tracking with the operation applied.</returns>
        ITrackedEntityLoadResult<TLoadResult> RecordDeleteOperation();

        /// <summary>
        /// Appends a create or update operation to the tracking based on the current state of the tracking.
        /// </summary>
        /// <param name="entity">The entity that is created or deleted.</param>
        /// <returns>A tracking with the operation applied.</returns>
        /// <exception cref="ArgumentNullException">Thrown is <paramref name="entity"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="entity"/> is either not assignable to the entity-type as specified by 
        /// <see cref="EntityIdentifier"/>, is a delegate instance or is a value-type instance.
        /// </exception>
        ITrackedEntityLoadResult<TLoadResult> RecordCreateOrUpdateOperation(object entity);

        /// <summary>
        /// Returns an entity load-result with the tracked operations applied.
        /// </summary>
        /// <returns>A <typeparamref name="TLoadResult"/> with the tracked operations applied.</returns>
        TLoadResult ApplyRecordedOperations();
    }
}