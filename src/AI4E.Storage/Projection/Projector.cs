/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    public sealed class Projector : IProjector
    {
        private readonly ConcurrentDictionary<Type, ITypedProjector> _typedProjectors = new ConcurrentDictionary<Type, ITypedProjector>();
        private readonly IServiceProvider _serviceProvider;

        public Projector(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        private Projector<TSource> GetTypedProjector<TSource>()
        {
            return (Projector<TSource>)_typedProjectors.GetOrAdd(typeof(TSource), _ => new Projector<TSource>(_serviceProvider));
        }

        public Task<IHandlerRegistration<IProjection<TSource, TProjection>>> RegisterProjectionAsync<TSource, TProjection>(IContextualProvider<IProjection<TSource, TProjection>> projectionProvider, CancellationToken cancellation)
        {
            if (projectionProvider == null)
                throw new ArgumentNullException(nameof(projectionProvider));

            return GetTypedProjector<TSource>().RegisterProjectionAsync(projectionProvider, cancellation);
        }

        public Task ProjectAsync(Type sourceType, object source, CancellationToken cancellation)
        {
            if (sourceType == null)
                throw new ArgumentNullException(nameof(sourceType));

            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!sourceType.IsAssignableFrom(source.GetType()))
                throw new ArgumentException($"The argument '{nameof(source)}' must be of the type specified by '{nameof(sourceType)}' or a derived type.");


            var currType = sourceType;
            var tasks = new List<Task>();

            do
            {
                Debug.Assert(currType != null);

                if (TryGetTypedProjector(currType, out var projector))
                {
                    tasks.Add(projector.ProjectAsync(source, cancellation));
                }
            }
            while (!currType.IsInterface && (currType = currType.BaseType) != null);

            return Task.WhenAll(tasks);
        }

        public Task ProjectAsync<TSource>(TSource source, CancellationToken cancellation)
        {
            return ProjectAsync(typeof(TSource), source, cancellation);
        }

        private bool TryGetTypedProjector(Type type, out ITypedProjector typedProjector)
        {
            Debug.Assert(type != null);

            var result = _typedProjectors.TryGetValue(type, out typedProjector);

            Debug.Assert(!result || typedProjector != null);
            Debug.Assert(!result || typedProjector.SourceType == type);
            return result;
        }
    }

    internal interface ITypedProjector
    {
        Task ProjectAsync(object source, CancellationToken cancellation);
        Type SourceType { get; }
    }

    internal sealed class Projector<TSource> : ITypedProjector
    {
        private readonly ConcurrentDictionary<Type, ITypedProjector<TSource>> _typedProjectors = new ConcurrentDictionary<Type, ITypedProjector<TSource>>();
        private readonly IServiceProvider _serviceProvider;

        public Type SourceType => typeof(TSource);

        public Projector(IServiceProvider serviceProvider)
        {
            _serviceProvider = serviceProvider;
        }

        private Projector<TSource, TProjection> GetTypedProjector<TProjection>()
        {
            return (Projector<TSource, TProjection>)_typedProjectors.GetOrAdd(typeof(TProjection), _ => new Projector<TSource, TProjection>(_serviceProvider));
        }

        public Task<IHandlerRegistration<IProjection<TSource, TProjection>>> RegisterProjectionAsync<TProjection>(IContextualProvider<IProjection<TSource, TProjection>> projectionProvider, CancellationToken cancellation)
        {
            if (projectionProvider == null)
                throw new ArgumentNullException(nameof(projectionProvider));

            return GetTypedProjector<TProjection>().RegisterProjectionAsync(projectionProvider, cancellation);
        }

        public Task ProjectAsync(object source, CancellationToken cancellation)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            
            return Task.WhenAll(GetTypedProjectors().Select(p => p.ProjectAsync(source, cancellation)));
        }

        public Task ProjectAsync(TSource source, CancellationToken cancellation)
        {
            return ProjectAsync(source, cancellation);
        }

        private IEnumerable<ITypedProjector<TSource>> GetTypedProjectors()
        {
            return _typedProjectors.Values;
        }
    }

    internal interface ITypedProjector<TSource>
    {
        Task ProjectAsync(object source, CancellationToken cancellation);
        Type ProjectionType { get; }
    }

    internal sealed class Projector<TSource, TProjection> : ITypedProjector<TSource>
    {
        private readonly HandlerRegistry<IProjection<TSource, TProjection>> _projections;
        private readonly IServiceProvider _serviceProvider;

        public Projector(IServiceProvider serviceProvider)
        {
            _projections = new HandlerRegistry<IProjection<TSource, TProjection>>();
            _serviceProvider = serviceProvider;
        }

        public async Task<IHandlerRegistration<IProjection<TSource, TProjection>>> RegisterProjectionAsync( // TODO: This does not need to be async
            IContextualProvider<IProjection<TSource, TProjection>> projectionProvider, 
            CancellationToken cancellation)
        {
            return HandlerRegistration.CreateRegistration(_projections, projectionProvider);
        }

        public Task ProjectAsync(object source, CancellationToken cancellation)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));

            if (!(source is TSource typedSource))
            {
                throw new ArgumentException($"The argument must be of type '{ typeof(TSource).FullName }' or a derived type.", nameof(source));
            }

            return ProjectAsync(typedSource, cancellation);
        }

        public Task ProjectAsync(TSource source, CancellationToken cancellation)
        {
            var dataStore = _serviceProvider.GetRequiredService<IDataStore>();

            return Task.WhenAll(_projections.Handlers.Select(p => ProjectSingleAsync(source, dataStore, p, cancellation)));
        }

        private async Task ProjectSingleAsync(TSource source, IDataStore dataStore, IContextualProvider<IProjection<TSource, TProjection>> projectionProvider, CancellationToken cancellation)
        {
            var projection = projectionProvider.ProvideInstance(_serviceProvider);

            Debug.Assert(projection != null);

            var res = await projection.ProjectAsync(source);

            await dataStore.StoreAsync(res, cancellation);
        }

        public Type ProjectionType => typeof(TProjection);
    }
}
