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
using System.Collections.Generic;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents a provider of projections.
    /// </summary>
    public interface IProjectionProvider
    {
        /// <summary>
        /// Returns an ordered collection of projection registrations for the specified source type.
        /// </summary>
        /// <param name="sourceType">The source type.</param>
        /// <returns>An ordered collection of projection registrations for <paramref name="sourceType"/>.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceType"/> is <c>null</c>.</exception>
        IReadOnlyList<IProjectionRegistration> GetProjectionRegistrations(Type sourceType);

        /// <summary>
        /// Returns an ordered collection of all projection registrations.
        /// </summary>
        /// <returns>An ordered collection of projection registrations.</returns>
        IReadOnlyList<IProjectionRegistration> GetProjectionRegistrations();
    }
}
