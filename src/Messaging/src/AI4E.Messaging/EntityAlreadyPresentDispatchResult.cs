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
using System.Diagnostics.CodeAnalysis;
using System.Runtime.Serialization;
using AI4E.Utils;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation that failed due to an id conflict.
    /// </summary>
    [Serializable]
    public class EntityAlreadyPresentDispatchResult : FailureDispatchResult
    {
        private readonly SerializableType? _entityType;
        public static string DefaultMessage { get; } = "An entity with the specified id is already present.";

        #region C'tor

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
        public EntityAlreadyPresentDispatchResult(string message, IReadOnlyDictionary<string, object?> resultData)
            : base(message, resultData)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="EntityAlreadyPresentDispatchResult"/> type.
        /// </summary>
        /// <param name="entityType">The type of resource, that an id-conflict occured at.</param>
        /// <param name="id">The stringified id that conflicted.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if <paramref name="entityType"/> is null.
        /// </exception>
        public EntityAlreadyPresentDispatchResult(Type entityType, string? id)
            : base(FormatDefaultMessage(entityType, id))
        {
            _entityType = new SerializableType(entityType.GetUnqualifiedTypeName(), entityType);
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
            _entityType = new SerializableType(entityType.GetUnqualifiedTypeName(), entityType);
        }

        #endregion

        #region ISerializable

        protected EntityAlreadyPresentDispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
            SerializableType? entityType;
            string? id;

            try
            {
#pragma warning disable CA1062
                entityType = serializationInfo.GetValue(
                    "EntityType", typeof(SerializableType?)) as SerializableType?;
#pragma warning restore CA1062

                id = serializationInfo.GetString(nameof(Id));
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }

            _entityType = entityType;
            Id = id;
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
#pragma warning disable CA1062
            info.AddValue("EntityType", _entityType, typeof(SerializableType?));
            info.AddValue(nameof(Id), Id, typeof(string));
#pragma warning restore CA1062
        }

        #endregion

        /// <summary>
        /// Gets the unqualified type-name of the resource that an id-conflict occured at.
        /// </summary>
        public string? EntityTypeName => _entityType?.TypeName;

        /// <summary>
        /// Gets the stringified id that conflicted.
        /// </summary>
        public string? Id { get; }

        /// <summary>
        /// Tries to load the type of resource that an id-conflict occured at.
        /// </summary>
        /// <param name="entityType">Contains the resource type if the call is succeeds.</param>
        /// <returns>True if the call suceeded, false otherwise.</returns>
        public bool TryGetEntityType([NotNullWhen(true)] out Type? entityType)
        {
            if (_entityType is null)
            {
                entityType = null;
                return false;
            }

            return _entityType.Value.TryGetType(out entityType);
        }

        private static string FormatDefaultMessage(Type entityType, string? id)
        {
            if (id == null)
                return FormatDefaultMessage(entityType);

            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

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
