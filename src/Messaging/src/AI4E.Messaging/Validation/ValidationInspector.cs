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
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using AI4E.Utils;
using AI4E.Utils.Async;

namespace AI4E.Messaging.Validation
{
    /// <summary>
    /// Inspects validation members of message handlers.
    /// </summary>
    public sealed class ValidationInspector : TypeMemberInspector<ValidationDescriptor>
    {
        [ThreadStatic] private static ValidationInspector? _instance;

        /// <summary>
        /// Gets the singleton <see cref="ValidationInspector"/> instance for the current thread.
        /// </summary>
        public static ValidationInspector Instance
        {
            get
            {
                if (_instance == null)
                    _instance = new ValidationInspector();

                return _instance;
            }
        }

        private ValidationInspector() { }

        /// <inheritdoc/>
        protected override bool IsSychronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name == "Validate";
        }

        /// <inheritdoc/>
        protected override bool IsAsynchronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return member.Name == "ValidateAsync";
        }

        /// <inheritdoc/>
        protected override ValidationDescriptor CreateDescriptor(Type type, MethodInfo member, Type parameterType)
        {
            return new ValidationDescriptor(parameterType, member);
        }

        /// <inheritdoc/>
        protected override bool IsValidReturnType(AwaitableTypeDescriptor returnTypeDescriptor)
        {
            if (returnTypeDescriptor.ResultType == typeof(void))
                return true;

            if (typeof(IEnumerable<ValidationResult>).IsAssignableFrom(returnTypeDescriptor.ResultType))
                return true;

            if (typeof(ValidationResult) == returnTypeDescriptor.ResultType)
                return true;

            if (typeof(ValidationResultsBuilder) == returnTypeDescriptor.ResultType)
                return true;

            return false;
        }
    }

    /// <summary>
    /// Describes a single validation in a message handler.
    /// </summary>
    public readonly struct ValidationDescriptor
    {
        private static readonly ConcurrentDictionary<Type, ImmutableDictionary<Type, ValidationDescriptor>> _descriptorsCache
                   = new ConcurrentDictionary<Type, ImmutableDictionary<Type, ValidationDescriptor>>();

        /// <summary>
        /// Create a new instance of the <see cref="ValidationDescriptor"/> type.
        /// </summary>
        /// <param name="parameterType">A <see cref="Type"/> that specifies the validated message type.</param>
        /// <param name="member">A <see cref="MethodInfo"/> instance that specifies the member.</param>
        public ValidationDescriptor(
            Type parameterType,
            MethodInfo member)
        {
            // TODO: Use the parameter order to TypeMemberInspector`1.CreateDescriptor
            if (parameterType == null)
                throw new ArgumentNullException(nameof(parameterType));

            if (member == null)
                throw new ArgumentNullException(nameof(member));

            ParameterType = parameterType;
            Member = member;
        }

        /// <summary>
        /// Gets a <see cref="Type"/> that specifies the validated message type.
        /// </summary>
        public Type ParameterType { get; }

        /// <summary>
        /// Gets a <see cref="MethodInfo"/> instance that specifies the member.
        /// </summary>
        public MethodInfo Member { get; }

        /// <summary>
        /// Attempty to retrieve the validation descriptor for the specified message handler and handled type.
        /// </summary>
        /// <param name="messageHandlerType">The message handler type.</param>
        /// <param name="handledType">The type the message handler handles.</param>
        /// <param name="descriptor">Contains the valdidation descriptor if the operation succeeds.</param>
        /// <returns>True if the operation is successful, false otherwise.</returns>
        public static bool TryGetDescriptor(Type messageHandlerType, Type handledType, out ValidationDescriptor descriptor)
        {
            if (_descriptorsCache.TryGetValue(messageHandlerType, out var descriptors))
            {
                return descriptors.TryGetValue(handledType, out descriptor);
            }

            var members = ValidationInspector.Instance.InspectType(messageHandlerType);
            var duplicates = members.GroupBy(p => p.ParameterType).Where(p => p.Count() > 1);

            if (duplicates.Any(p => p.Key == handledType))
            {
                throw new InvalidOperationException("Ambigous validation");
            }

            var descriptorL = members.Where(p => p.ParameterType == handledType);
            var result = false;

            if (descriptorL.Any())
            {
                descriptor = descriptorL.First();
                result = true;
            }
            else
            {
                descriptor = default;
            }

            if (!duplicates.Any())
            {
                _descriptorsCache.TryAdd(messageHandlerType, members.ToImmutableDictionary(p => p.ParameterType));
            }

            return result;
        }
    }
}
