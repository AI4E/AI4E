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

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Represents the registration of a projection.
    /// </summary>
    public interface IProjectionRegistration
    {
        /// <summary>
        /// Creates an instance of the registered projection within the scope of the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider that is used to obtain handler specific services.</param>
        /// <returns>The created instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        IProjection CreateProjection(IServiceProvider serviceProvider);

        /// <summary>
        /// Gets the type of the source elements the projection projects.
        /// </summary>
        Type SourceType { get; }

        /// <summary>
        /// Gets the type of the target elements the projection projects to.
        /// </summary>
        Type TargetType { get; }

        /// <summary>
        /// Tries to retrieve the <see cref="ProjectionDescriptor"/> that the instance was created with.
        /// </summary>
        /// <param name="descriptor">Contains the <see cref="ProjectionDescriptor"/> if the operation suceeds.</param>
        /// <returns>True if the operation was successful, false otherwise.</returns>
        bool TryGetDescriptor(out ProjectionDescriptor descriptor);
    }

    /// <summary>
    /// Represents the registration of a projection for the specifies source and target types.
    /// </summary>
    /// <typeparam name="TSource">The type of the source elements the projection projects</typeparam>
    /// <typeparam name="TTarget">The type of the target elements the projection projects to.</typeparam>
    public interface IProjectionRegistration<TSource, TTarget> : IProjectionRegistration
        where TSource : class
        where TTarget : class
    {
        /// <summary>
        /// Creates an instance of the registered projection within the scope of the specified service provider.
        /// </summary>
        /// <param name="serviceProvider">The service provider that is used to obtain handler specific services.</param>
        /// <returns>The created instance.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="serviceProvider"/> is null.</exception>
        new IProjection<TSource, TTarget> CreateProjection(IServiceProvider serviceProvider);

        IProjection IProjectionRegistration.CreateProjection(IServiceProvider serviceProvider)
        {
            return CreateProjection(serviceProvider);
        }

        Type IProjectionRegistration.SourceType => typeof(TSource);
        Type IProjectionRegistration.TargetType => typeof(TTarget);
    }
}
