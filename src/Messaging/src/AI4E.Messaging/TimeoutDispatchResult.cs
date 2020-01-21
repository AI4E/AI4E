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
using System.Runtime.Serialization;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation timeout.
    /// </summary>
    [Serializable]
    public class TimeoutDispatchResult : FailureDispatchResult
    {
        public static string DefaultMessage { get; } = "The message was not handled in due time";

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="TimeoutDispatchResult"/> type.
        /// </summary>
        public TimeoutDispatchResult() : base(DefaultMessage) { }

        /// <summary>
        /// Creates a new instance of the <see cref="TimeoutDispatchResult"/> type.
        /// </summary>
        ///  <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public TimeoutDispatchResult(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="TimeoutDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public TimeoutDispatchResult(string message, IReadOnlyDictionary<string, object?> resultData)
            : base(message, resultData)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="TimeoutDispatchResult"/> type.
        /// </summary>
        /// <param name="dueTime">The due time that the message could not be handled in.</param>
        public TimeoutDispatchResult(DateTime dueTime) : base(DefaultMessage)
        {
            DueTime = dueTime;
        }

        #endregion

        #region ISerializable

        protected TimeoutDispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
            DateTime? dueTime;

            try
            {
#pragma warning disable CA1062
                dueTime = serializationInfo.GetValue(nameof(DueTime), typeof(DateTime?)) as DateTime?;
#pragma warning restore CA1062
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }

            DueTime = dueTime;
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
#pragma warning disable CA1062
            info.AddValue(nameof(DueTime), DueTime, typeof(DateTime?));
#pragma warning restore CA1062
        }

        #endregion

        /// <summary>
        /// Gets the due time that the message could not be handled in.
        /// </summary>
        public DateTime? DueTime { get; }
    }
}
