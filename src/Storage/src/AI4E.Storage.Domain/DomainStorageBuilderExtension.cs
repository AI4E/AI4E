/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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

using Microsoft.Extensions.DependencyInjection;
using System;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Contains extensions for the <see cref="IDomainStorageBuilder"/> type.
    /// </summary>
    public static class DomainStorageBuilderExtension
    {
        /// <summary>
        /// Configures the domain storage options.
        /// </summary>
        /// <param name="builder">The domain storage builder.</param>
        /// <param name="configuration">
        /// An <see cref="Action{DomainStorageOptions}"/> used to configure the domain storage options.
        /// </param>
        /// <returns>The domain storage builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="builder"/> or <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public static IDomainStorageBuilder Configure(
            this IDomainStorageBuilder builder,
            Action<DomainStorageOptions> configuration)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            _ = builder.Services.Configure(configuration);

            return builder;
        }

        public static IDomainStorageBuilder ConfigureCommitAttemptProccessors(
            this IDomainStorageBuilder builder,
            Action<ICommitAttemptProcessorRegistry, IServiceProvider> configuration)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            ICommitAttemptProcessorRegistry Decorator(
                ICommitAttemptProcessorRegistry registry,
                IServiceProvider serviceProvider)
            {
                configuration(registry, serviceProvider);
                return registry;
            }

            builder.Services.Decorate<ICommitAttemptProcessorRegistry>(Decorator);

            return builder;
        }

        public static IDomainStorageBuilder ConfigureCommitAttemptProccessors(
            this IDomainStorageBuilder builder,
            Action<ICommitAttemptProcessorRegistry> configuration)
        {
            if (builder is null)
                throw new ArgumentNullException(nameof(builder));

            ICommitAttemptProcessorRegistry Decorator(
                ICommitAttemptProcessorRegistry registry,
                IServiceProvider serviceProvider)
            {
                configuration(registry);
                return registry;
            }

            builder.Services.Decorate<ICommitAttemptProcessorRegistry>(Decorator);

            return builder;
        }
    }
}
