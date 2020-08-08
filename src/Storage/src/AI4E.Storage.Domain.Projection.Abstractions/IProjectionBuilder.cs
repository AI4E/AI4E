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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Represents a projection builder.
    /// </summary>
    public interface IProjectionBuilder
    {
        IDomainStorageBuilder DomainStorageBuilder { get; }

        /// <summary>
        /// Configures the projection services.
        /// </summary>
        /// <param name="configuration">The service configuration.</param>
        /// <returns>The projection builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="configuration"/> is <c>null</c>.</exception>
        IProjectionBuilder ConfigureServices(Action<IServiceCollection> configuration);
    }
}
