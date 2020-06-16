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
using Newtonsoft.Json;

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a failed message dispatch operation.
    /// </summary>
    [Serializable]
    public class FailureDispatchResult : DispatchResult
    {
        #region C'tors

        /// <summary>
        /// Creates a new instance of the <see cref="FailureDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public FailureDispatchResult(string message, IReadOnlyDictionary<string, object?> resultData)
            : base(false, message, resultData)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="FailureDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public FailureDispatchResult(string message)
            : base(false, message)
        { }

        /// <summary>
        /// Creates a new instance of the <see cref="FailureDispatchResult"/> type.
        /// </summary>
        /// <param name="exception">The exception that caused the message dispatch operation to fail.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception"/> is <c>null</c>.</exception>
        public FailureDispatchResult(Exception exception) : this(FormatMessage(exception))
        {
            Exception = exception;
        }

        /// <summary>
        /// Creates a new instance of the <see cref="FailureDispatchResult"/> type.
        /// </summary>
        public FailureDispatchResult() : this("Unknown failure.") { }

        #endregion

        #region ISerializable

        protected FailureDispatchResult(SerializationInfo serializationInfo, StreamingContext streamingContext)
            : base(serializationInfo, streamingContext)
        {
            Exception? exception;

            try
            {
#pragma warning disable CA1062
                exception = serializationInfo.GetValue(nameof(Exception), typeof(Exception)) as Exception;
#pragma warning restore CA1062
            }
            catch (InvalidCastException exc)
            {
                // TODO: More specific error message
                throw new SerializationException("Cannot deserialize dispatch result.", exc);
            }


            Exception = exception;
        }

        protected override void GetObjectData(SerializationInfo info, StreamingContext context)
        {
            base.GetObjectData(info, context);
#pragma warning disable CA1062
            info.AddValue(nameof(Exception), Exception, typeof(Exception));
#pragma warning restore CA1062
        }

        #endregion

        private static string FormatMessage(Exception exception)
        {
            if (exception == null)
                throw new ArgumentNullException(nameof(exception));

            return "An unhandled exception occured: " + exception.Message;
        }

        /// <summary>
        /// Gets the exception that caused the message dispatch operation to fail.
        /// </summary>
        public Exception? Exception { get; }

        /// <inheritdoc/>
        public override bool IsSuccess => false;
    }
}
