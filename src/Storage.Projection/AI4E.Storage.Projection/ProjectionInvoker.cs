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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AI4E.Utils;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    /// <summary>
    /// Contains factory methods to create generic projection invokers.
    /// </summary>
    public static class ProjectionInvoker
    {
        private static readonly Type _projectionInvokerTypeDefinition = typeof(ProjectionInvoker<,>);
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>> _factories
            = new ConcurrentDictionary<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>>();

        private static readonly Func<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>> _factoryBuilderCache = BuildFactory;

        /// <summary>
        /// Creates a <see cref="ProjectionInvoker{TSource, TTarget}"/> from the specified parameters.
        /// </summary>
        /// <param name="projectionDescriptor">The descriptor that specified the projection type and member.</param>
        /// <param name="serviceProvider">A <see cref="IServiceProvider"/> used to obtain services.</param>
        /// <returns>The created <see cref="ProjectionInvoker{TSource, TTarget}"/>.</returns>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="serviceProvider"/> is <c>null</c>.
        /// </exception>
        public static IProjection CreateInvoker(
            ProjectionDescriptor projectionDescriptor,
            IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var handlerType = projectionDescriptor.HandlerType;
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, handlerType);

            Debug.Assert(handler != null);
            return CreateInvokerInternal(handler, projectionDescriptor, serviceProvider);
        }

        private static IProjection CreateInvokerInternal(
            object handler,
            ProjectionDescriptor projectionDescriptor,
            IServiceProvider serviceProvider)
        {
            var sourceType = projectionDescriptor.SourceType;
            var targetType = projectionDescriptor.TargetType;

            var factory = _factories.GetOrAdd((sourceType, targetType), _factoryBuilderCache);
            return factory(handler, projectionDescriptor, serviceProvider);
        }

        private static Func<object, ProjectionDescriptor, IServiceProvider, IProjection> BuildFactory(
           (Type sourceType, Type targetType) _)
        {
            var projectionInvokerType = _projectionInvokerTypeDefinition.MakeGenericType(_.sourceType, _.targetType);
            var ctor = projectionInvokerType.GetConstructor(
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic,
                Type.DefaultBinder,
                types: new[] { typeof(object), typeof(ProjectionDescriptor), typeof(IServiceProvider) },
                modifiers: null);

            Debug.Assert(ctor != null);

            var handlerParameter = Expression.Parameter(typeof(object), "handler");
            var projectionDescriptorParameter = Expression.Parameter(typeof(ProjectionDescriptor), "projectionDescriptor");
            var serviceProviderParameter = Expression.Parameter(typeof(IServiceProvider), "serviceProvider");
            var ctorCall = Expression.New(ctor, handlerParameter, projectionDescriptorParameter, serviceProviderParameter);
            var convertedInvoker = Expression.Convert(ctorCall, typeof(IProjection));
            var lambda = Expression.Lambda<Func<object, ProjectionDescriptor, IServiceProvider, IProjection>>(
               convertedInvoker, handlerParameter, projectionDescriptorParameter, serviceProviderParameter);

            return lambda.Compile();
        }
    }

    /// <summary>
    /// Represents projections as <see cref="IProjection"/>.
    /// </summary>
    /// <typeparam name="TSource">The tyep of projection source.</typeparam>
    /// <typeparam name="TTarget">The type of projection target.</typeparam>
    public sealed class ProjectionInvoker<TSource, TTarget> : IProjection<TSource, TTarget>
        where TSource : class
        where TTarget : class
    {
        private readonly object _handler;
        private readonly ProjectionDescriptor _projectionDescriptor;
        private readonly IServiceProvider _serviceProvider;

        // This is needed for reflection (dynamic compiled code).
        // If this is changed, adapt the caller in ProjectionInvoker.BuildFactory
        [MethodImpl(MethodImplOptions.PreserveSig)]
#pragma warning disable IDE0051
        internal ProjectionInvoker(
#pragma warning restore IDE0051
            object handler,
            ProjectionDescriptor projectionDescriptor,
            IServiceProvider serviceProvider)
        {
            _handler = handler;
            _projectionDescriptor = projectionDescriptor;
            _serviceProvider = serviceProvider;
        }

        /// <inheritdoc/>
        public async IAsyncEnumerable<TTarget> ProjectAsync(
            TSource source,
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            if (source == null && !_projectionDescriptor.ProjectNonExisting)
            {
                yield break;
            }

            var member = _projectionDescriptor.Member;
            Debug.Assert(member != null);
            var invoker = TypeMemberInvoker.GetInvoker(member);

            object ResolveParameter(ParameterInfo parameter)
            {
                if (parameter.ParameterType == typeof(IServiceProvider))
                {
                    return _serviceProvider;
                }
                else if (parameter.ParameterType == typeof(CancellationToken))
                {
                    return cancellation;
                }
                else if (ParameterDefaultValue.TryGetDefaultValue(parameter, out var defaultValue))
                {
                    return _serviceProvider.GetService(parameter.ParameterType) ?? defaultValue;
                }
                else
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            var result = await invoker.InvokeAsync(_handler, source, ResolveParameter);

            if (result == null)
            {
                yield break;
            }

            if (_projectionDescriptor.MultipleResults)
            {
                if (result is IAsyncEnumerable<TTarget> asyncEnumerable)
                {
                    await foreach(var target in asyncEnumerable)
                    {
                        yield return target;
                    }
                }
                else if (result is IEnumerable<TTarget> enumerable)
                {
                    foreach (var target in enumerable)
                    {
                        yield return target;
                    }
                }
            }
            else if (result is TTarget projectionResult)
            {
                yield return projectionResult;
            }

            // This is possible if the projection descriptor we are working with
            // does not match the projection.
            // This may be the case f.e. if the descriptors states that we are handling
            // a single targets projection but it is a multiple targets projection actually.

            // TODO: Do we want to throw here or silently ignore this?
            //       At least, we have to log a warning.
        }

        Type IProjection.SourceType => typeof(TSource);
        Type IProjection.TargetType => typeof(TTarget);

        IAsyncEnumerable<object> IProjection.ProjectAsync(object source, CancellationToken cancellation)
        {
            return ProjectAsync(source as TSource, cancellation);
        }
    }
}
