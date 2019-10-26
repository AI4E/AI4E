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
using Newtonsoft.Json;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation that failed because the message cannot be dispatched.
    /// </summary>
    public sealed class DispatchFailureDispatchResult : FailureDispatchResult
    {
        [JsonProperty("MessageType")]
        private readonly SerializableType _messageType;

#pragma warning disable IDE0051
        [JsonConstructor]
        private DispatchFailureDispatchResult(
            SerializableType messageType,
            string message,
            IReadOnlyDictionary<string, object?> resultData)
            : base(message, resultData)
        {
            _messageType = messageType;
        }
#pragma warning restore IDE0051

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchFailureDispatchResult"/> type.
        /// </summary>
        /// <param name="messageType">The type of message that cannot be dispatched.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="messageType"/> is <c>null</c>.</exception>
        public DispatchFailureDispatchResult(Type messageType) : base(FormatDefaultMessage(messageType?.GetUnqualifiedTypeName()))
        {
            if (messageType is null)
                throw new ArgumentNullException(nameof(messageType));

            _messageType = new SerializableType(messageType.GetUnqualifiedTypeName(), messageType);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchFailureDispatchResult"/> type.
        /// </summary>
        /// <param name="messageType">The type of message that cannot be dispatched.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="messageType"/> or <paramref name="message"/> is <c>null</c>.
        /// </exception>
        public DispatchFailureDispatchResult(Type messageType, string message) : base(message)
        {
            _messageType = new SerializableType(messageType.GetUnqualifiedTypeName(), messageType);
        }

        /// <summary>
        /// Creates a new instance of the <see cref="DispatchFailureDispatchResult"/> type.
        /// </summary>
        /// <param name="messageType">The type of message that cannot be dispatched.</param>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if any of <paramref name="messageType"/>, <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public DispatchFailureDispatchResult(Type messageType, string message, IReadOnlyDictionary<string, object?> resultData)
            : base(message, resultData)
        {
            _messageType = new SerializableType(messageType.GetUnqualifiedTypeName(), messageType);
        }

        /// <summary>
        /// Gets the unqualified type-name of message that cannot be dispatched.
        /// </summary>
        public string MessageTypeName => _messageType.TypeName;

        /// <summary>
        /// Tries to load the type of message that cannot be dispatched.
        /// </summary>
        /// <param name="entityType">Contains the message type if the call succeeds.</param>
        /// <returns>True if the call suceeded, false otherwise.</returns>
        public bool TryGetMessageType([NotNullWhen(true)] out Type? entityType)
        {
            return _messageType.TryGetType(out entityType);
        }

        private static string FormatDefaultMessage(string? messageTypeName)
        {
            return $"The message of type '{messageTypeName}' cannot be dispatched.";
        }
    }
}
