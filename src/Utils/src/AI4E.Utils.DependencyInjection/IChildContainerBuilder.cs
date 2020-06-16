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

namespace AI4E.Utils.DependencyInjection
{
    /// <summary>
    /// Represents a builder that can be used to construct child containers of a dependency-injection container.
    /// </summary>
    public interface IChildContainerBuilder
    {
        /// <summary>
        /// Constructs a child container of the dependency-injection container the builder was retrieved from 
        /// and returns a service-provider that can be used to retrieve services from the created child container.
        /// </summary>
        /// <param name="serviceConfiguration">The service configuration for the child container.</param>
        /// <returns>
        /// A <see cref="IChildServiceProvider"/> that can be used to retrieve services from the created child container.
        /// </returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceConfiguration"/> is <c>null</c>.
        /// </exception>
        IChildServiceProvider CreateChildContainer(Action<IServiceCollection> serviceConfiguration);
    }

    /// <summary>
    /// Represents a service-provider that can be used to retrieve services from a child dependency injection container.
    /// </summary>
    public interface IChildServiceProvider : IServiceProvider, IDisposable, IAsyncDisposable { }
}
