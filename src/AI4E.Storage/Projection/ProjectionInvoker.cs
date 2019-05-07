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
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection;
using System.Threading;
using AI4E.Handler;
using Microsoft.Extensions.DependencyInjection;
using static System.Diagnostics.Debug;

namespace AI4E.Storage.Projection
{
    public sealed class ProjectionInvoker<TSource, TProjection> : IProjection<TSource, TProjection>
        where TSource : class
        where TProjection : class
    {
        private readonly object _handler;
        private readonly ProjectionDescriptor _projectionDescriptor;
        private readonly IServiceProvider _serviceProvider;

        private ProjectionInvoker(object handler,
                                  ProjectionDescriptor projectionDescriptor,
                                  IServiceProvider serviceProvider)
        {
            _handler = handler;
            _projectionDescriptor = projectionDescriptor;
            _serviceProvider = serviceProvider;
        }

        public async IAsyncEnumerable<TProjection> ProjectAsync(TSource source, CancellationToken cancellation)
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
                    var enumerable = result as IEnumerable<TProjection>;
                    Assert(enumerable != null);

                    foreach (var singleResult in enumerable)
                    {
                        yield return singleResult;
                    }
                }
                else
                {
                    var projectionResult = result as TProjection;
                    Assert(projectionResult != null);

                    yield return projectionResult;
                }
            }
        }

        internal sealed class Provider : IContextualProvider<IProjection<TSource, TProjection>>
        {
            private readonly Type _type;
            private readonly ProjectionDescriptor _projectionDescriptor;

            public Provider(Type type, ProjectionDescriptor projectionDescriptor)
            {
                _type = type;
                _projectionDescriptor = projectionDescriptor;
            }

            public IProjection<TSource, TProjection> ProvideInstance(IServiceProvider serviceProvider)
            {
                if (serviceProvider == null)
                    throw new ArgumentNullException(nameof(serviceProvider));

                var handler = ActivatorUtilities.CreateInstance(serviceProvider, _type);

                Debug.Assert(handler != null);

                return new ProjectionInvoker<TSource, TProjection>(handler, _projectionDescriptor, serviceProvider);
            }
        }
    }
}
