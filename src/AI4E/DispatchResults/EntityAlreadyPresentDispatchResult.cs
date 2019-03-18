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
using Newtonsoft.Json;

namespace AI4E.DispatchResults
{
    /// <summary>
    /// Describes the result of a message dispatch operation that failed due to an id conflict.
    /// </summary>
    public class EntityAlreadyPresentDispatchResult : FailureDispatchResult
    {
        internal const string DefaultMessage = "An entity with the specified id is already present.";

#pragma warning disable IDE0051
        [JsonConstructor]
        private EntityAlreadyPresentDispatchResult(
            Type entityType,
            string id,
            string message,
            IReadOnlyDictionary<string, object> resultData)
            : base(message, resultData)
        {
            EntityType = entityType;
            Id = id;
        }
#pragma warning restore IDE0051

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        public EntityAlreadyPresentDispatchResult() : base(DefaultMessage) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        ///  <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public EntityAlreadyPresentDispatchResult(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public EntityAlreadyPresentDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(message, resultData)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        /// <param name="entityType">The type of resource, that an id-conflict occured at.</param>
        /// <param name="id">The stringified id that conflicted.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="entityType"/> or <paramref name="id"/> is null.
        /// </exception>
        public EntityAlreadyPresentDispatchResult(Type entityType, string id)
            : base(FormatDefaultMessage(entityType, id))
        {
            EntityType = entityType;
            Id = id;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        /// <param name="entityType">The type of resource, that an id-conflict occured at.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="entityType"/> is null.</exception>
        public EntityAlreadyPresentDispatchResult(Type entityType)
            : base(FormatDefaultMessage(entityType))
        {
            EntityType = entityType;
        }

        /// <summary>
        /// Gets the type of resource, that an id-conflict occured at.
        /// </summary>
        public Type EntityType { get; }

        /// <summary>
        /// Gets the stringified id that conflicted.
        /// </summary>
        public string Id { get; }

        private static string FormatDefaultMessage(Type entityType, string id)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            return $"An entity of type'{entityType}' with the id '{id}' is already present.";
        }

        private static string FormatDefaultMessage(Type entityType)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            return $"An entity of type'{entityType}' with the specified id is already present.";
        }
    }
}
