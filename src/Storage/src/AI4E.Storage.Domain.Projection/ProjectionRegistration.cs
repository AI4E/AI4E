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
    /// Represents the registration of a projection.
    /// </summary>
    public sealed class ProjectionRegistration : IProjectionRegistration
    {
        private readonly Func<IServiceProvider, IProjection> _factory;
        private readonly ProjectionDescriptor? _descriptor;

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionRegistration"/> type.
        /// </summary>
        /// <param name="factory">A factory function that is used to obtain the projection.</param>
        /// <param name="descriptor">A descriptor describing the projection.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="descriptor"/> does not specify both, an entity and a projection target type.
        /// </exception>
        public ProjectionRegistration(
            Func<IServiceProvider, IProjection> factory,
            in ProjectionDescriptor descriptor)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            if (descriptor.EntityType is null || descriptor.TargetType is null)
                throw new ArgumentException("The descriptor must specify both, an entity and a projection target type.", nameof(descriptor));

            EntityType = descriptor.EntityType;
            TargetType = descriptor.TargetType;
            _factory = factory;
            _descriptor = descriptor;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionRegistration"/> type.
        /// </summary>
        /// <param name="entityType">The type of entity.</param>
        /// <param name="targetType">The type of projection target.</param>
        /// <param name="factory">A factory function that is used to obtain the projection.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="factory"/>, <paramref name="entityType"/>
        /// or <paramref name="targetType"/> is <c>null</c>.
        /// </exception>
        public ProjectionRegistration(
            Type entityType,
            Type targetType,
            Func<IServiceProvider, IProjection> factory)
        {
            if (entityType is null)
                throw new ArgumentNullException(nameof(entityType));

            if (targetType is null)
                throw new ArgumentNullException(nameof(targetType));

            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            EntityType = entityType;
            TargetType = targetType;
            _factory = factory;
        }

        /// <inheritdoc />
        public IProjection CreateProjection(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var result = _factory(serviceProvider);

            if (result == null)
                throw new InvalidOperationException("The projection provided must not be null.");

            if (result.EntityType != EntityType)
                throw new InvalidOperationException($"The projection provided must project objects of type {EntityType}.");

            if (result.TargetType != TargetType)
                throw new InvalidOperationException($"The projection provided must project to objects of type {TargetType}.");

            return result;
        }

        /// <inheritdoc />
        public Type EntityType { get; }

        /// <inheritdoc />
        public Type TargetType { get; }

        /// <inheritdoc />
        public bool TryGetDescriptor(out ProjectionDescriptor descriptor)
        {
            descriptor = _descriptor.GetValueOrDefault();
            return _descriptor.HasValue;
        }
    }

    /// <summary>
    /// Represents the registration of a projection.
    /// </summary>
    /// <typeparam name="TEntity">The type of entity.</typeparam>
    /// <typeparam name="TTarget">The type of projection target.</typeparam>
    public sealed class ProjectionRegistration<TEntity, TTarget> : IProjectionRegistration<TEntity, TTarget>
        where TEntity : class
        where TTarget : class
    {
        private readonly Func<IServiceProvider, IProjection<TEntity, TTarget>> _factory;
        private readonly ProjectionDescriptor? _descriptor;

        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionRegistration"/> type.
        /// </summary>
        /// <param name="factory">A factory function that is used to obtain the projection.</param>
        /// <param name="descriptor">A descriptor describing the projection.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="factory"/> is <c>null</c>.</exception>
        /// <exception cref="ArgumentException">
        /// Thrown if <paramref name="descriptor"/> does not specify the same entity and projection target types
        /// as <typeparamref name="TEntity"/> and <typeparamref name="TTarget"/> respectively.
        /// </exception>
        public ProjectionRegistration(
            Func<IServiceProvider, IProjection<TEntity, TTarget>> factory,
            in ProjectionDescriptor? descriptor = null)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            if (descriptor != null)
            {
                if (descriptor.Value.EntityType != typeof(TEntity))
                    throw new ArgumentException($"The descriptor must specify the entity type {typeof(TEntity)}.");

                if (descriptor.Value.TargetType != typeof(TTarget))
                    throw new ArgumentException($"The descriptor must specify the target type {typeof(TTarget)}.");
            }

            _factory = factory;
            _descriptor = descriptor;
        }

        /// <inheritdoc />
        public IProjection<TEntity, TTarget> CreateProjection(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var result = _factory(serviceProvider);

            if (result == null)
                throw new InvalidOperationException("The projection provided must not be null.");

            return result;
        }

        /// <inheritdoc />
        public bool TryGetDescriptor(out ProjectionDescriptor descriptor)
        {
            descriptor = _descriptor.GetValueOrDefault();
            return _descriptor.HasValue;
        }
    }
}
