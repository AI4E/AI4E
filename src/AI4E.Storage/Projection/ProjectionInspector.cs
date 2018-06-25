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
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using AI4E.Internal;

namespace AI4E.Storage.Projection
{
    internal sealed class ProjectionInspector
    {
        private readonly Type _type;

        public ProjectionInspector(Type type)
        {
            if (type == null)
                throw new ArgumentNullException(nameof(type));

            _type = type;
        }

        public IEnumerable<ProjectionDescriptor> GetDescriptors()
        {
            var members = _type.GetMethods();
            var descriptors = new List<ProjectionDescriptor>();

            foreach (var member in members)
            {
                if (TryGetHandlingMember(member, out var descriptor))
                {
                    descriptors.Add(descriptor);
                }
            }

            return descriptors;
        }

        private bool TryGetHandlingMember(MethodInfo member, out ProjectionDescriptor descriptor)
        {
            var parameters = member.GetParameters();

            if (parameters.Length == 0)
            {
                descriptor = default;
                return false;
            }

            if (parameters.Any(p => p.ParameterType.IsByRef))
            {
                descriptor = default;
                return false;
            }

            if (member.IsGenericMethod || member.IsGenericMethodDefinition)
            {
                descriptor = default;
                return false;
            }

            if (member.IsDefined<NoProjectionMemberAttribute>())
            {
                descriptor = default;
                return false;
            }

            var sourceType = parameters[0].ParameterType;
            var memberAttribute = member.GetCustomAttribute<ProjectionMemberAttribute>();

            var projectionType = default(Type);

            // Synchronous handler
            if ((member.Name.StartsWith("Project") && !member.Name.EndsWith("Async") || memberAttribute != null) &&
                (member.ReturnType == typeof(void) || !typeof(Task).IsAssignableFrom(member.ReturnType)))
            {
                projectionType = member.ReturnType;
            }

            // Asynchronous handler
            else if ((member.Name.StartsWith("Project") || memberAttribute != null) &&
                (typeof(Task).IsAssignableFrom(member.ReturnType)))
            {
                projectionType = member.ReturnType == typeof(Task) ? typeof(void) : member.ReturnType.GetGenericArguments()[0];
            }
            else
            {
                descriptor = default;
                return false;
            }

            var multipleResults = false;
            Type multipleResultsType = null;

            if (projectionType.IsGenericType && projectionType.GetGenericTypeDefinition() == typeof(IEnumerable<>))
            {
                multipleResultsType = projectionType;
                projectionType = multipleResultsType.GetGenericArguments().First();
                multipleResults = true;
            }

            if (memberAttribute != null)
            {
                if (memberAttribute.SourceType != null)
                {
                    if (!sourceType.IsAssignableFrom(memberAttribute.SourceType))
                    {
                        throw new InvalidOperationException();
                    }

                    sourceType = memberAttribute.SourceType;
                }

                if (memberAttribute.ProjectionType != null)
                {
                    if (member.ReturnType == typeof(void) || !memberAttribute.ProjectionType.IsAssignableFrom(projectionType))
                    {
                        throw new InvalidOperationException();
                    }

                    projectionType = memberAttribute.ProjectionType;
                }

                if (memberAttribute.MultipleResults != null)
                {
                    if (!multipleResults && (bool)memberAttribute.MultipleResults)
                    {
                        throw new InvalidOperationException();
                    }

                    if (multipleResults && !(bool)memberAttribute.MultipleResults)
                    {
                        projectionType = multipleResultsType;
                        multipleResults = false;
                    }
                }
            }

            descriptor = new ProjectionDescriptor(sourceType, projectionType, multipleResults, member);
            return true;
        }
    }
}
