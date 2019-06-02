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
    public sealed class ProjectionRegistration : IProjectionRegistration
    {
        private readonly Func<IServiceProvider, IProjection> _factory;
        private readonly ProjectionDescriptor? _descriptor;

        public ProjectionRegistration(
            Type sourceType,
            Type projectionType,
            Func<IServiceProvider, IProjection> factory,
            in ProjectionDescriptor? descriptor = null)
        {
            if (sourceType is null)
                throw new ArgumentNullException(nameof(sourceType));

            if (projectionType is null)
                throw new ArgumentNullException(nameof(projectionType));

            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            SourceType = sourceType;
            ProjectionType = projectionType;
            _factory = factory;
            _descriptor = descriptor;
        }

        public IProjection CreateProjection(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var result = _factory(serviceProvider);

            if (result == null)
                throw new InvalidOperationException("The projection provided must not be null.");

            if (result.SourceType != SourceType)
                throw new InvalidOperationException($"The projection provided must projection objects of type {SourceType}.");

            if (result.ProjectionType != ProjectionType)
                throw new InvalidOperationException($"The projection provided must projection to objects of type {ProjectionType}.");

            return result;
        }

        public Type SourceType { get; }

        public Type ProjectionType { get; }

        /// <inheritdoc />
        public bool TryGetDescriptor(out ProjectionDescriptor descriptor)
        {
            descriptor = _descriptor.GetValueOrDefault();
            return _descriptor.HasValue;
        }
    }

    public sealed class ProjectionRegistration<TSource, TProjection> : IProjectionRegistration<TSource, TProjection>
        where TSource : class
        where TProjection : class
    {
        private readonly Func<IServiceProvider, IProjection<TSource, TProjection>> _factory;
        private readonly ProjectionDescriptor? _descriptor;

        public ProjectionRegistration(
            Func<IServiceProvider, IProjection<TSource, TProjection>> factory,
            in ProjectionDescriptor? descriptor = null)
        {
            if (factory is null)
                throw new ArgumentNullException(nameof(factory));

            _factory = factory;
            _descriptor = descriptor;
        }

        public IProjection<TSource, TProjection> CreateProjection(IServiceProvider serviceProvider)
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

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS

        IProjection IProjectionRegistration.CreateProjection(IServiceProvider serviceProvider)
        {
            return CreateProjection(serviceProvider);
        }

        Type IProjectionRegistration.SourceType => typeof(TSource);

        Type IProjectionRegistration.ProjectionType => typeof(TProjection);

#endif
    }
}
