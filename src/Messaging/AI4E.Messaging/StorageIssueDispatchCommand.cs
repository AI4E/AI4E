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

namespace AI4E.Messaging
{
    /// <summary>
    /// Describes the result of a message dispatch operation that failed due to an error in the storage subsystem.
    /// </summary>
    public class StorageIssueDispatchResult : FailureDispatchResult
    {
        public static string DefaultMessage { get; } = "Unknown failure in the storage subsystem";

#pragma warning disable IDE0051
        [JsonConstructor]
        private StorageIssueDispatchResult(string message, Exception exception, IReadOnlyDictionary<string, object> resultData)
            : base(message, exception, resultData)
        { }
#pragma warning restore IDE0051

        /// <summary>
        /// Creates a new instance of the <see cref="StorageIssueDispatchResult"/> type.
        /// </summary>
        public StorageIssueDispatchResult() : base(DefaultMessage) { }

        /// <summary>
        /// Creates a new instance of the <see cref="StorageIssueDispatchResult"/> type.
        /// </summary>
        ///  <param name="exception">The exception that caused the message dispatch operation to fail.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="exception"/> is <c>null</c>.</exception>
        public StorageIssueDispatchResult(Exception exception) : base(exception) { }

        /// <summary>
        /// Creates a new instance of the <see cref="StorageIssueDispatchResult"/> type.
        /// </summary>
        ///  <param name="message">A message describing the message dispatch result.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="message"/> is <c>null</c>.</exception>
        public StorageIssueDispatchResult(string message) : base(message) { }

        /// <summary>
        /// Creates a new instance of the <see cref="StorageIssueDispatchResult"/> type.
        /// </summary>
        /// <param name="message">A message describing the message dispatch result.</param>
        /// <param name="resultData">A collection of key value pairs that represent additional result data.</param>
        /// <exception cref="ArgumentNullException">
        /// Thrown if either <paramref name="message"/> or <paramref name="resultData"/> is <c>null</c>.
        /// </exception>
        public StorageIssueDispatchResult(string message, IReadOnlyDictionary<string, object> resultData)
            : base(message, resultData)
        { }
    }
}
