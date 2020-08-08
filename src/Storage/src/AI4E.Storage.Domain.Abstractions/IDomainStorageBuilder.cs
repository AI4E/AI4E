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
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a builder that can be used to configure the domain storage system.
    /// </summary>
    public interface IDomainStorageBuilder
    {
        /// <summary>
        /// Gets the underlying <see cref="IStorageBuilder"/>.
        /// </summary>
        IStorageBuilder StorageBuilder { get; }

        public IDomainStorageBuilder ConfigureServices(Action<IServiceCollection> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

#pragma warning disable CS0618
            configuration(Services);
#pragma warning restore CS0618

            return this;
        }

        /// <summary>
        /// Get the service-collection used to configure services.
        /// </summary>
        [Obsolete("Use ConfigureServices")]
        IServiceCollection Services => StorageBuilder.Services;
    }
}
