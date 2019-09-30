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
using AI4E.Utils.Async;

namespace AI4E.Messaging.Handler
{
    /// <summary>
    /// A base type for type member inspectors.
    /// </summary>
    /// <typeparam name="TMemberDescriptor">The type of descriptor that is used for inspected members.</typeparam>
    public abstract class TypeMemberInspector<TMemberDescriptor> : TypeMemberInspector<TMemberDescriptor, Type>
    {
        /// <summary>
        /// Return the parameter type of the specified member.
        /// </summary>
        /// <param name="type">The type that is inspected.</param>
        /// <param name="member">The member thats parameter type shall be obtained.</param>
        /// <param name="parameters">A collection that represents the members parameters.</param>
        /// <returns>The parameter type of <paramref name="member"/>.</returns>
        protected override Type GetParameters(
            Type type,
            MethodInfo member,
            IReadOnlyList<ParameterInfo> parameters,
            AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return parameters[0].ParameterType;
        }
    }

    /// <summary>
    /// A base type for type member inspectors.
    /// </summary>
    /// <typeparam name="TMemberDescriptor">The type of descriptor that is used for inspected members.</typeparam>
    /// <typeparam name="TParameters">The type of parameters.</typeparam>
    public abstract class TypeMemberInspector<TMemberDescriptor, TParameters>
    {
        /// <summary>
        /// Inspected the members of the specified type and returns a collection of descriptors for the inspected members.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>A collection of descriptors for the inspected members.</returns>
        public IEnumerable<TMemberDescriptor> InspectType(Type type)
        {
            var members = type.GetMethods();
            var descriptors = new List<TMemberDescriptor>();

            foreach (var member in members)
            {
                if (TryGetDescriptor(type, member, out var descriptor))
                {
                    descriptors.Add(descriptor);
                }
            }

            return descriptors;
        }

        private bool TryGetDescriptor(Type type, MethodInfo member, out TMemberDescriptor descriptor)
        {
            descriptor = default;
            var parameters = member.GetParameters();

            if (parameters.Length == 0)
            {
                return false;
            }

            if (parameters.Any(p => !IsValidParameter(p)))
            {
                return false;
            }

            if (!IsValidMember(member))
            {
                return false;
            }

            var returnTypeDescriptor = AwaitableTypeDescriptor.GetTypeDescriptor(member.ReturnType);

            if (!IsValidReturnType(returnTypeDescriptor))
            {
                return false;
            }

            var p = GetParameters(type, member, parameters, returnTypeDescriptor);

            if (IsSychronousMember(member, returnTypeDescriptor) && !returnTypeDescriptor.IsAwaitable)
            {
                descriptor = CreateDescriptor(type, member, p);
                return true;
            }

            if (IsAsynchronousMember(member, returnTypeDescriptor) && returnTypeDescriptor.IsAwaitable)
            {
                descriptor = CreateDescriptor(type, member, p);
                return true;
            }

            return false;
        }

        /// <summary>
        /// Returns a boolen value indicating whether the specified member is a valid.
        /// </summary>
        /// <param name="member">The member to inspect.</param>
        /// <returns>True if <paramref name="member"/> is valid, false otherwise.</returns>
        protected virtual bool IsValidMember(MethodInfo member)
        {
            return !member.IsGenericMethod && !member.IsGenericMethodDefinition;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified parameter is valid.
        /// </summary>
        /// <param name="parameter">The parameter to inspect.</param>
        /// <returns>True if <paramref name="parameter"/> is valid, false otherwise.</returns>
        protected virtual bool IsValidParameter(ParameterInfo parameter)
        {
            return !parameter.ParameterType.IsByRef;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified return type is valid.
        /// </summary>
        /// <param name="returnTypeDescriptor">An <see cref="AwaitableTypeDescriptor"/> that described the return type.</param>
        /// <returns>True if the return type described by <paramref name="returnTypeDescriptor"/> is valid, false otherwise.</returns>
        protected virtual bool IsValidReturnType(AwaitableTypeDescriptor returnTypeDescriptor)
        {
            return true;
        }

        /// <summary>
        /// Return the parameter type of the specified member.
        /// </summary>
        /// <param name="type">The type that is inspected.</param>
        /// <param name="member">The member thats parameter type shall be obtained.</param>
        /// <param name="parameters">A collection that represents the members parameters.</param>
        /// <returns>The parameter type of <paramref name="member"/>.</returns>
        protected abstract TParameters GetParameters(
            Type type,
            MethodInfo member,
            IReadOnlyList<ParameterInfo> parameters,
            AwaitableTypeDescriptor returnTypeDescriptor);

        /// <summary>
        /// Returns a boolean value indicating whether the specified member is a synchronous result.
        /// </summary>
        /// <param name="member">The member to inspect.</param>
        /// <param name="returnTypeDescriptor">The <see cref="AwaitableTypeDescriptor"/> that described the members result type.</param>
        /// <returns>True if <paramref name="member"/> is a synchronous result, false otherwise.</returns>
        protected abstract bool IsSychronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor);

        /// <summary>
        /// Returns a boolean value indicating whether the specified member is aa asynchronous result.
        /// </summary>
        /// <param name="member">The member to inspect.</param>
        /// <param name="returnTypeDescriptor">The <see cref="AwaitableTypeDescriptor"/> that described the members result type.</param>
        /// <returns>True if <paramref name="member"/> is an asynchronous result, false otherwise.</returns>
        protected abstract bool IsAsynchronousMember(MethodInfo member, AwaitableTypeDescriptor returnTypeDescriptor);

        /// <summary>
        /// Creates a descriptor for the specified member.
        /// </summary>
        /// <param name="type">The type that is inspected.</param>
        /// <param name="member">The member that a descriptor shall be created for.</param>
        /// <param name="parameters">The parameters.</param>
        /// <returns>The created member descriptor.</returns>
        protected abstract TMemberDescriptor CreateDescriptor(Type type, MethodInfo member, TParameters parameters);
    }
}
