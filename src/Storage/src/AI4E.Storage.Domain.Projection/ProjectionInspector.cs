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
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using AI4E.Utils;
using AI4E.Utils.Async;

namespace AI4E.Storage.Domain.Projection
{
    /// <summary>
    /// Inspected the members of a projection declaring type.
    /// </summary>
    public sealed class ProjectionInspector : TypeMemberInspector<ProjectionDescriptor, ProjectionParameters>
    {
        [ThreadStatic] private static ProjectionInspector _instance;

        /// <summary>
        /// Gets the singleton <see cref="ProjectionInspector"/> instance for the current thread.
        /// </summary>
        public static ProjectionInspector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ProjectionInspector();

                return _instance;
            }
        }

        private ProjectionInspector() { }

        /// <inheritdoc/>
        protected override ProjectionDescriptor CreateDescriptor(
            Type type,
            MethodInfo member,
            ProjectionParameters parameters)
        {
            return new ProjectionDescriptor(
                type,
                parameters.SourceType,
                parameters.TargetType,
                member,
                parameters.MultipleResults,
                parameters.ProjectNonExisting);
        }

        private static bool IsAssignableToEnumerable(Type type, out Type elementType)
        {
            static bool IsEnumerable(Type type)
            {
                if (!type.IsGenericType)
                    return false;

                var typeDefinition = type.GetGenericTypeDefinition();

                return typeDefinition == typeof(IEnumerable<>)
                    || typeDefinition == typeof(IAsyncEnumerable<>);
            }

            if (IsEnumerable(type))
            {
                elementType = type.GetGenericArguments().First();
                return true;
            }

            elementType = null;
            var candidates = type.GetInterfaces().Where(p => IsEnumerable(p)).Select(p => type.GetGenericArguments().First());
            var bestMatch = default(Type);

            foreach (var candidate in candidates)
            {
                if (bestMatch == null)
                    bestMatch = candidate;

                if (bestMatch.IsAssignableFrom(candidate))
                    bestMatch = candidate;

                if (candidate.IsAssignableFrom(bestMatch))
                    continue;

                // The type implements IEnumerable for multiple element types.
                // As no one of them supersedes them all in the type hierarchy,
                // we have a conflict here.
                elementType = null;
                return false;
            }

            if (bestMatch != null)
            {
                elementType = bestMatch;
                return true;
            }

            return false;
        }

        /// <inheritdoc/>
        protected override ProjectionParameters GetParameters(
            Type type,
            MethodInfo member,
            IReadOnlyList<ParameterInfo> parameters,
            AwaitableTypeDescriptor returnTypeDescriptor)
        {
            var sourceType = parameters[0].ParameterType;
            var targetType = returnTypeDescriptor.ResultType;
            var projectNonExisting = false;
            var multipleResults = false;
            Type multipleResultsType = null;

            if (IsAssignableToEnumerable(targetType, out var elementType))
            {
                multipleResultsType = targetType;
                targetType = elementType;
                multipleResults = true;
            }

            var memberAttribute = member.GetCustomAttribute<ProjectionAttribute>(inherit: true);

            if (memberAttribute == null)
            {
                memberAttribute = type.GetCustomAttribute<ProjectionAttribute>(inherit: true);
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

                if (memberAttribute.TargetType != null)
                {
                    if (member.ReturnType == typeof(void) || !memberAttribute.TargetType.IsAssignableFrom(targetType))
                    {
                        throw new InvalidOperationException();
                    }

                    targetType = memberAttribute.TargetType;
                }

                if (memberAttribute.MultipleResults.IsValid() &&
                    memberAttribute.MultipleResults != MultipleProjectionResults.Unset)
                {
                    var explicitMultipleResults = memberAttribute.MultipleResults == MultipleProjectionResults.MultipleResults;

                    if (!multipleResults && explicitMultipleResults)
                    {
                        throw new InvalidOperationException();
                    }

                    if (multipleResults && !explicitMultipleResults)
                    {
                        targetType = multipleResultsType;
                        multipleResults = false;
                    }
                }

                projectNonExisting = memberAttribute.ProjectNonExisting;
            }

            return new ProjectionParameters(sourceType, targetType, multipleResults, projectNonExisting);
        }

        /// <inheritdoc/>
        protected override bool IsValidReturnType(AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return true;
        }

        /// <inheritdoc/>
        protected override bool IsValidMember(MethodInfo member)
        {
            if (!base.IsValidMember(member))
                return false;

            // There is defined a NoMessageHandlerAttribute on the member somewhere in the inheritance hierarchy.
            if (member.IsDefined<NoProjectionAttribute>(inherit: true))
            {
                // The member on the current type IS decorated with a NoProjectionAttribute OR
                // The member on the current type IS NOT decorated with a ProjectionAttribute
                // TODO: Test this.
                if (member.IsDefined<NoProjectionAttribute>(inherit: false) || !member.IsDefined<ProjectionAttribute>(inherit: false))
                {
                    return false;
                }
            }

            return true;
        }

        /// <inheritdoc/>
        protected override bool IsValidParameter(ParameterInfo parameter)
        {
            return !parameter.ParameterType.IsByRef;
        }

        /// <inheritdoc/>
        protected override bool IsAsynchronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name.StartsWith("Project") && member.Name.EndsWith("Async") || member.IsDefined<ProjectionAttribute>(inherit: true);
        }

        /// <inheritdoc/>
        protected override bool IsSychronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name.StartsWith("Project") && !member.Name.EndsWith("Async") || member.IsDefined<ProjectionAttribute>(inherit: true);
        }
    }

    /// <summary>
    /// Represents the parameters of a projection.
    /// </summary>
    public readonly struct ProjectionParameters
    {
        /// <summary>
        /// Creates a new instance of the <see cref="ProjectionParameters"/> type.
        /// </summary>
        /// <param name="sourceType">The projection source type.</param>
        /// <param name="targetType">The projection target type.</param>
        /// <param name="multipleResults">
        /// A boolean value indicating whether the projection projections to multiple targets.
        /// </param>
        /// <param name="projectNonExisting">
        /// A boolean value indicating whether the projection shall be invoked for non-existing sources.
        /// </param>
        public ProjectionParameters(Type sourceType, Type targetType, bool multipleResults, bool projectNonExisting)
        {
            SourceType = sourceType;
            TargetType = targetType;
            MultipleResults = multipleResults;
            ProjectNonExisting = projectNonExisting;
        }

        /// <summary>
        /// Gets the projection source type.
        /// </summary>
        public Type SourceType { get; }

        /// <summary>
        /// Gets the projection target type.
        /// </summary>
        public Type TargetType { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the projection projections to multiple targets.
        /// </summary>
        public bool MultipleResults { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the projection shall be invoked for non-existing sources.
        /// </summary>
        public bool ProjectNonExisting { get; }
    }
}
