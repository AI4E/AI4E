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
    /// Contains extensions for the <see cref="ITrackedEntity"/> type.
    /// </summary>
    public static class TrackedEntityExtension
    {
        /// <summary>
        /// Returns a boolean value indicating whether the specified tracked entity is modified.
        /// </summary>
        /// <param name="trackedEntity">The tracked entity.</param>
        /// <returns>
        /// True if the track state of <paramref name="trackedEntity"/> indicates modification, false otherwise.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="trackedEntity"/> is <c>null</c>.
        /// </exception>
        public static bool IsModified(this ITrackedEntity trackedEntity)
        {
            if (trackedEntity is null)
                throw new ArgumentNullException(nameof(trackedEntity));

            return trackedEntity.TrackState switch
            {
                EntityTrackState.Created => true,
                EntityTrackState.Deleted => true,
                EntityTrackState.Updated => true,
                _ => false
            };
        }

        /// <summary>
        /// Returns the entity identifier from the specified tracked entity.
        /// </summary>
        /// <param name="trackedEntity">The tracked entity.</param>
        /// <returns>The <see cref="EntityIdentifier"/> constructed from <paramref name="trackedEntity"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="trackedEntity"/> is <c>null</c>.
        /// </exception>
        public static EntityIdentifier GetEntityIdentifier(this ITrackedEntity trackedEntity)
        {
            if (trackedEntity is null)
                throw new ArgumentNullException(nameof(trackedEntity));

            return trackedEntity.OriginalEntityLoadResult.EntityIdentifier;
        }

        /// <summary>
        /// Returns the entity descriptor from the specified tracked entity.
        /// </summary>
        /// <param name="trackedEntity">The tracked entity.</param>
        /// <returns>
        /// The <see cref="EntityDescriptor"/> constructed from <paramref name="trackedEntity"/> 
        /// if <paramref name="trackedEntity"/> tracks a non-null entity, <c>null</c> otherwise..
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="trackedEntity"/> is <c>null</c>.
        /// </exception>
        public static EntityDescriptor? GetEntityDescriptor(this ITrackedEntity trackedEntity)
        {
            if (trackedEntity is null)
                throw new ArgumentNullException(nameof(trackedEntity));

            if (trackedEntity.Entity is null)
                return null;

            return new EntityDescriptor(
                trackedEntity.OriginalEntityLoadResult.EntityIdentifier.EntityType,
                trackedEntity.Entity);
        }
    }
}
