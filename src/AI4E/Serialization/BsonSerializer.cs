/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        BsonSerializer.cs 
 * Types:           (1) AI4E.Serialization.BsonSerializer
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   04.01.2018 
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
using System.Collections;
using System.IO;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Bson;

namespace AI4E.Serialization
{
    public class BsonSerializer : JsonSerializer
    {
        private readonly ILogger _logger;

        public BsonSerializer(params Type[] knownTypes) : base(knownTypes) { }

        protected BsonSerializer(ILogger logger, params Type[] knownTypes) : base(logger, knownTypes)
        {
            _logger = logger;
        }

        public BsonSerializer(Logger<BsonSerializer> logger, params Type[] knownTypes) : this(logger as ILogger, knownTypes) { }

        public override void Serialize<T>(Stream output, T graph)
        {
            var writer = new BsonDataWriter(output) { DateTimeKindHandling = DateTimeKind.Utc };
            Serialize(writer, graph);
        }

        public override T Deserialize<T>(Stream input)
        {
            var reader = new BsonDataReader(input, IsArray(typeof(T)), DateTimeKind.Utc);
            return Deserialize<T>(reader);
        }

        private bool IsArray(Type type)
        {
            var isArray = typeof(IEnumerable).IsAssignableFrom(type) && !typeof(IDictionary).IsAssignableFrom(type);

            _logger?.LogTrace(Messages.TypeIsArray, type, isArray);

            return isArray;
        }
    }
}