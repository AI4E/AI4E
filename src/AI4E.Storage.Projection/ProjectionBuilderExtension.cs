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
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Contains extensions for the <see cref="IProjectionBuilder"/> type.
    /// </summary>
    public static class ProjectionBuilderExtension
    {
        /// <summary>
        /// Configres the registered projections.
        /// </summary>
        /// <param name="projectionBuilder">The projection builder.</param>
        /// <param name="configuration">The projection configuration.</param>
        /// <returns>The projection builder.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="configuration"/> is <c>null</c>.
        /// </exception>
        public static IProjectionBuilder ConfigureProjections(
            this IProjectionBuilder projectionBuilder,
            Action<IProjectionRegistry, IServiceProvider> configuration)
        {
            if (configuration is null)
                throw new ArgumentNullException(nameof(configuration));

            void ConfigureProjections(IServiceCollection services)
            {
                services.Decorate<IProjectionRegistry>((registry, provider) =>
                {
                    configuration(registry, provider);
                    return registry;
                });
            }

            projectionBuilder.ConfigureServices(ConfigureProjections);

            return projectionBuilder;
        }

        /// <summary>
        /// Uses the specified type of projection source processor factory.
        /// </summary>
        /// <typeparam name="TSourceProcessorFactory">
        /// The type of projection source processor factory.
        /// </typeparam>
        /// <param name="projectionBuilder">The projection builder.</param>
        /// <returns>The projection builder.</returns>
        public static IProjectionBuilder UseSourceProcessor<TSourceProcessorFactory>(
            this IProjectionBuilder projectionBuilder)
            where TSourceProcessorFactory : class, IProjectionSourceProcessorFactory
        {
            static void ConfigureProjections(IServiceCollection services)
            {
                services.AddSingleton<IProjectionSourceProcessorFactory, TSourceProcessorFactory>();
            }

            projectionBuilder.ConfigureServices(ConfigureProjections);

            return projectionBuilder;
        }

        /// <summary>
        /// Uses the specified projection source processor factory.
        /// </summary>
        /// <param name="projectionBuilder">The projection builder.</param>
        /// <param name="sourceProcessorFactory">The projection source processor factory.</param>
        /// <returns>The projection builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="sourceProcessorFactory"/> is <c>null</c>.</exception>
        public static IProjectionBuilder UseSourceProcessor(
            this IProjectionBuilder projectionBuilder,
            IProjectionSourceProcessorFactory sourceProcessorFactory)
        {
            if (sourceProcessorFactory is null)
                throw new ArgumentNullException(nameof(sourceProcessorFactory));

            void ConfigureProjections(IServiceCollection services)
            {
                services.AddSingleton(sourceProcessorFactory);
            }

            projectionBuilder.ConfigureServices(ConfigureProjections);

            return projectionBuilder;
        }

        /// <summary>
        /// Uses the specified type of projection target processor factory.
        /// </summary>
        /// <typeparam name="TTargetProcessorFactory">
        /// The type of projection target processor factory.
        /// </typeparam>
        /// <param name="projectionBuilder">The projection builder.</param>
        /// <returns>The projection builder.</returns>
        public static IProjectionBuilder UseTargetProcessor<TTargetProcessorFactory>(
            this IProjectionBuilder projectionBuilder)
            where TTargetProcessorFactory : class, IProjectionTargetProcessorFactory
        {
            static void ConfigureProjections(IServiceCollection services)
            {
                services.AddSingleton<IProjectionTargetProcessorFactory, TTargetProcessorFactory>();
            }

            projectionBuilder.ConfigureServices(ConfigureProjections);

            return projectionBuilder;
        }

        /// <summary>
        /// Uses the specified projection target processor factory.
        /// </summary>
        /// <param name="projectionBuilder">The projection builder.</param>
        /// <param name="targetProcessorFactory">The projection target processor factory.</param>
        /// <returns>The projection builder.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="targetProcessorFactory"/> is <c>null</c>.</exception>
        public static IProjectionBuilder UseTargetProcessor(
            this IProjectionBuilder projectionBuilder,
            IProjectionTargetProcessorFactory targetProcessorFactory)
        {
            if (targetProcessorFactory is null)
                throw new ArgumentNullException(nameof(targetProcessorFactory));
            void ConfigureProjections(IServiceCollection services)
            {
                services.AddSingleton(targetProcessorFactory);
            }

            projectionBuilder.ConfigureServices(ConfigureProjections);

            return projectionBuilder;
        }
    }
}
