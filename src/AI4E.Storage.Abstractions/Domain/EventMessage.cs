/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EventMessage.cs 
 * Types:           (1) AI4E.Storage.Domain.EventMessage
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   13.06.2018 
 * --------------------------------------------------------------------------------------------------------------------
 */

/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
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

/* Based on
 * --------------------------------------------------------------------------------------------------------------------
 * NEventStore (https://github.com/NEventStore/NEventStore)
 * The MIT License
 * 
 * Copyright (c) 2013 Jonathan Oliver, Jonathan Matheus, Damian Hickey and contributors
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in
 * all copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
 * THE SOFTWARE.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Runtime.Serialization;
using Newtonsoft.Json;

namespace AI4E.Storage.Domain
{
    /// <summary>
    /// Represents a single element in a stream of events.
    /// </summary>
    public sealed class EventMessage
    {
        /// <summary>
        /// Initializes a new instance of the EventMessage class.
        /// </summary>
        [JsonConstructor]
        public EventMessage(object body, ImmutableDictionary<string, object> headers)
        {
            if (body == null)
                throw new ArgumentNullException(nameof(body));

            if (headers == null)
                throw new ArgumentNullException(nameof(headers));

            Body = body;
            Headers = headers;
        }

        public EventMessage(object body, IEnumerable<KeyValuePair<string, object>> headers)
            : this(body, headers as ImmutableDictionary<string, object> ?? headers?.ToImmutableDictionary())
        { }

        public EventMessage(object body) : this(body, ImmutableDictionary<string, object>.Empty) { }

        /// <summary>
        /// Gets the metadata which provides additional, unstructured information about this message.
        /// </summary>
        [JsonProperty]
        public ImmutableDictionary<string, object> Headers { get; }

        /// <summary>
        /// Gets or sets the actual event message body.
        /// </summary>
        [JsonProperty]
        public object Body { get; }
    }
}
