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
using AI4E.Utils;
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    /// <summary>
    /// Describes the result of a message dispatch operation that failed due to a non-found entity.
    /// </summary>
    public class EntityNotFoundDispatchResult : NotFoundDispatchResult
    {
        internal const string DefaultMessage = "An entity with the specified id cannot be not found.";

#pragma warning disable IDE0051
        [JsonConstructor]
        private EntityNotFoundDispatchResult(
            string entityTypeName,
            string id,
            string message,
            IReadOnlyDictionary<string, object> resultData)
            : base(message, resultData)
        {
            EntityTypeName = entityTypeName;
            Id = id;
        }
#pragma warning restore IDE0051

        /// <summary>
        /// Creates a new instance of the <see cref="EntityNotFoundDispatchResult"/> type.
        /// </summary>
        public EntityNotFoundDispatchResult() : base(DefaultMessage) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityNotFoundDispatchResult"/> type.
        /// </summary>
        ///  <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public EntityNotFoundDispatchResult(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityNotFoundDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public EntityNotFoundDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(message, resultData)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityNotFoundDispatchResult"/> type.
        /// </summary>
        /// <param name="entityType">The type of resource that was not found.</param>
        /// <param name="id">The stringified id of resource that was not found.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="id"/> is null.
        /// </exception>
        public EntityNotFoundDispatchResult(Type entityType, string id)
            : base(FormatDefaultMessage(entityType, id))
        {
            EntityTypeName = entityType.GetUnqualifiedTypeName();
            Id = id;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityNotFoundDispatchResult"/> type.
        /// </summary>
        /// <param name="entityType">The type of resource that was not found.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityType"/> is null.</exception>
        public EntityNotFoundDispatchResult(Type entityType)
            : base(FormatDefaultMessage(entityType))
        {
            EntityTypeName = entityType.GetUnqualifiedTypeName();
        }

        private static string FormatDefaultMessage(Type entityType, string id)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            return $"An entity of type'{entityType}' with the id '{id}' cannot be not found.";
        }

        private static string FormatDefaultMessage(Type entityType)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return $"An entity of type'{entityType}' with the specified id cannot be not found.";
        }

        /// <summary>
        /// Gets the unqualified type-name of resource that was not found.
        /// </summary>
        public string EntityTypeName { get; }

        /// <summary>
        /// Gets the stringified id of resource that was not found.
        /// </summary>
        public string Id { get; }

        /// <summary>
        /// Tries to load the type of resource that was not found.
        /// </summary>
        /// <param name="entityType">Contains the resource type if the call is succeeds.</param>
        /// <returns>True if the call suceeded, false otherwise.</returns>
        public bool TryGetEntityType(out Type entityType)
        {
            if (EntityTypeName == null)
            {
                entityType = null;
                return false;
            }

            entityType = TypeLoadHelper.LoadTypeFromUnqualifiedName(EntityTypeName, throwIfNotFound: false);
            return entityType != null;
        }
    }
}
