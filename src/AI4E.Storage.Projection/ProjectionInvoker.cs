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
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Threading;
using AI4E.Handler;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public static class ProjectionInvoker
    {
        private static readonly Type _projectionInvokerTypeDefinition = typeof(ProjectionInvoker<,>);
        private static readonly ConcurrentDictionary<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>> _factories
            = new ConcurrentDictionary<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>>();

        private static readonly Func<(Type, Type), Func<object, ProjectionDescriptor, IServiceProvider, IProjection>> _factoryBuilderCache = BuildFactory;

        public static IProjection CreateInvoker(
            ProjectionDescriptor projectionDescriptor,
            IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            var handlerType = projectionDescriptor.HandlerType;
            var handler = ActivatorUtilities.CreateInstance(serviceProvider, handlerType);

            Assert(handler != null);
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

            Assert(ctor != null);

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
        private ProjectionInvoker(object handler,
#pragma warning restore IDE0051
                                  ProjectionDescriptor projectionDescriptor,
                                  IServiceProvider serviceProvider)
        {
            _handler = handler;
            _projectionDescriptor = projectionDescriptor;
            _serviceProvider = serviceProvider;
        }

        public async IAsyncEnumerable<TTarget> ProjectAsync(
            TSource source,
            CancellationToken cancellation)
        {
            if (source == null && !_projectionDescriptor.ProjectNonExisting)
            {
                yield break;
            }

            var member = _projectionDescriptor.Member;
            Assert(member != null);
            var invoker = HandlerActionInvoker.GetInvoker(member);

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
                else if (parameter.HasDefaultValue)
                {
                    return _serviceProvider.GetService(parameter.ParameterType) ?? parameter.DefaultValue;
                }
                else
                {
                    return _serviceProvider.GetRequiredService(parameter.ParameterType);
                }
            }

            object result;

            //try
            //{
            result = await invoker.InvokeAsync(_handler, source, ResolveParameter); // TODO: Await
            //}
            //catch (Exception exc)
            //{
            //    // TODO: What can we do here?
            //    // TODO: Log this and rethrow

            //    throw;
            //}

            if (result != null)
            {
                if (_projectionDescriptor.MultipleResults)
                {
                    var enumerable = result as IEnumerable<TTarget>;
                    Assert(enumerable != null);

                    foreach (var singleResult in enumerable)
                    {
                        yield return singleResult;
                    }
                }
                else
                {
                    var projectionResult = result as TTarget;
                    Assert(projectionResult != null);

                    yield return projectionResult;
                }
            }
        }

#if !SUPPORTS_DEFAULT_INTERFACE_METHODS
        Type IProjection.SourceType => typeof(TSource);
        Type IProjection.TargetType => typeof(TTarget);

        IAsyncEnumerable<object> IProjection.ProjectAsync(object source, CancellationToken cancellation)
        {
            return ProjectAsync(source as TSource, cancellation);
        }
#endif
    }
}
