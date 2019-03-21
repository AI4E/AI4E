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
using Newtonsoft.Json;

namespace AI4E.Validation
{
    /// <summary>
    /// A message base-type that is used to validate a message.
    /// </summary>
    public abstract class Validate
    {
        private protected Validate(Type messageType, object message)
        {
            MessageType = messageType;
            Message = message;
        }

        /// <summary>
        /// Gets the message type of the validation dispatch.
        /// </summary>
        [JsonIgnore]
        public Type MessageType { get; }

        /// <summary>
        /// Gets the message that shall be validated in the validation dispatch.
        /// </summary>
        [JsonIgnore]
        public object Message { get; }
    }

    /// <summary>
    /// A message type that is used to validate a message.
    /// </summary>
    /// <typeparam name="TMessage">The type of message to validate.</typeparam>
    public sealed class Validate<TMessage> : Validate
        where TMessage : class
    {
        /// <summary>
        /// Creates a new instance of the <see cref="Validate"/> type.
        /// </summary>
        /// <param name="message">The message that shall be validated in the validation dispatch.</param>
        [JsonConstructor]
        public Validate(TMessage message) : base(typeof(TMessage), message)
        {
            Message = message;
        }

        /// <summary>
        /// Gets the message that shall be validated in the validation dispatch.
        /// </summary>
        public new TMessage Message { get; }
    }
}
