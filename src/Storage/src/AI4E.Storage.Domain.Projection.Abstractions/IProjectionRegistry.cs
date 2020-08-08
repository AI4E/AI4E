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

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a registry where projections can be registered.
    /// </summary>
    public interface IProjectionRegistry
    {
        /// <summary>
        /// Registers a projection.
        /// </summary>
        /// <param name="projectionRegistration">The projection to register.</param>
        /// <returns>True, if the projection was registered, false if a projection of the specified type was already registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="projectionRegistration"/> is null.</exception>
        bool Register(IProjectionRegistration projectionRegistration);

        /// <summary>
        /// Unregisters a projection.
        /// </summary>
        /// <param name="projectionRegistration">The projection to unregister.</param>
        /// <returns>True, if the projection was unregistered, false if a projection of the specified type was not registered.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="projectionRegistration"/> is null.</exception>
        bool Unregister(IProjectionRegistration projectionRegistration);

        /// <summary>
        /// Creates a <see cref="IProjectionProvider"/> of the current snapshot of projection registrations.
        /// </summary>
        /// <returns>The created <see cref="IProjectionProvider"/>.</returns>
        IProjectionProvider ToProvider();
    }
}
