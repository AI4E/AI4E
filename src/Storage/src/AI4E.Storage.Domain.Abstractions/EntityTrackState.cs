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

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Defines constants that describe the track-state of an <see cref="ITrackedEntity"/>.
    /// </summary>
    public enum EntityTrackState
    {
        /// <summary>
        /// The entity is untracked.
        /// </summary>
        Untracked,

        /// <summary>
        /// The entity is not existent.
        /// </summary>
        NonExistent,

        /// <summary>
        /// The entity is unchanged.
        /// </summary>
        Unchanged,

        /// <summary>
        /// The entity is updates, hence modified.
        /// </summary>
        Updated,

        /// <summary>
        /// The entity is creates, hence modified.
        /// </summary>
        Created,

        /// <summary>
        /// The entity is deleted, hence modified.
        /// </summary>
        Deleted
    }
}
