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
using System.Linq;
using System.Runtime.Serialization;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace AI4E.Storage.Projection
{
    internal class ProjectionInvoker<TSource, TProjection> : IProjection<TSource, TProjection>
    {
        private readonly object _handler;
        private ProjectionDescriptor _projectionDescriptor;
        private IServiceProvider _serviceProvider;

        private ProjectionInvoker(object handler, ProjectionDescriptor projectionDescriptor, IServiceProvider serviceProvider)
        {
            _handler = handler;
            _projectionDescriptor = projectionDescriptor;
            _serviceProvider = serviceProvider;
        }

        public bool MultipleResults => _projectionDescriptor.MultipleResults;

        public ValueTask<TProjection> ProjectAsync(TSource source)
        {
            var member = _projectionDescriptor.Member;

            Debug.Assert(member != null);

            var parameters = member.GetParameters();

            var callingArgs = new object[parameters.Length];

            callingArgs[0] = source;

            for (var i = 1; i < callingArgs.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;

                object arg;

                if (parameterType.IsDefined<FromServicesAttribute>())
                {
                    arg = _serviceProvider.GetRequiredService(parameterType);
                }
                else
                {
                    arg = _serviceProvider.GetService(parameterType);

                    if (arg == null && parameterType.IsValueType)
                    {
                        arg = FormatterServices.GetUninitializedObject(parameterType);
                    }
                }

                callingArgs[i] = arg;
            }

            var result = member.Invoke(_handler, callingArgs);

            if (result is Task task)
            {
                if (_projectionDescriptor.MultipleResults)
                {
                    if (!(result is Task<IEnumerable<TProjection>> multProjTask))
                    {
                        throw new InvalidOperationException();
                    }

                    async ValueTask<TProjection> GetFirstOrDefault()
                    {
                        return (await multProjTask).FirstOrDefault();
                    }

                    return GetFirstOrDefault();
                }

                if (!(result is Task<TProjection> projTask))
                {
                    throw new InvalidOperationException();
                }

                return new ValueTask<TProjection>(projTask);
            }

            if (result == null)
            {
                return new ValueTask<TProjection>(default(TProjection));
            }

            if (_projectionDescriptor.MultipleResults)
            {
                if (!(result is IEnumerable<TProjection> multProj))
                {
                    throw new InvalidOperationException();
                }

                return new ValueTask<TProjection>(multProj.FirstOrDefault());
            }

            if (!(result is TProjection proj))
            {
                throw new InvalidOperationException();
            }

            return new ValueTask<TProjection>(proj);
        }

        public IAsyncEnumerable<TProjection> ProjectMultipleAsync(TSource source)
        {
            if (!_projectionDescriptor.MultipleResults)
            {
                return ProjectAsync(source).ToAsyncEnumerable();
            }

            var member = _projectionDescriptor.Member;

            Debug.Assert(member != null);

            var parameters = member.GetParameters();

            var callingArgs = new object[parameters.Length];

            callingArgs[0] = source;

            for (var i = 1; i < callingArgs.Length; i++)
            {
                var parameterType = parameters[i].ParameterType;

                object arg;

                if (parameterType.IsDefined<FromServicesAttribute>())
                {
                    arg = _serviceProvider.GetRequiredService(parameterType);
                }
                else
                {
                    arg = _serviceProvider.GetService(parameterType);

                    if (arg == null && parameterType.IsValueType)
                    {
                        arg = FormatterServices.GetUninitializedObject(parameterType);
                    }
                }

                callingArgs[i] = arg;
            }

            var result = member.Invoke(_handler, callingArgs);

            if (result is Task task)
            {

                if (!(result is Task<IEnumerable<TProjection>> multProjTask))
                {
                    throw new InvalidOperationException();
                }

                return multProjTask.ToAsyncEnumerable();
            }

            if (result == null)
            {
                return AsyncEnumerable.Empty<TProjection>();
            }

            if (!(result is IEnumerable<TProjection> multProj))
            {
                throw new InvalidOperationException();
            }

            return multProj.ToAsyncEnumerable();
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
