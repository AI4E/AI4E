/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        JsonSerializer.cs 
 * Types:           (1) AI4E.Serialization.JsonSerializer
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace AI4E.Serialization
{
    public class JsonSerializer : ISerializer
    {
        private readonly IEnumerable<Type> _knownTypes = new Type[] { };
        private readonly ILogger _logger;
        private readonly Newtonsoft.Json.JsonSerializer _typedSerializer = new Newtonsoft.Json.JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.All,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };
        private readonly Newtonsoft.Json.JsonSerializer _untypedSerializer = new Newtonsoft.Json.JsonSerializer
        {
            TypeNameHandling = TypeNameHandling.Auto,
            DefaultValueHandling = DefaultValueHandling.Ignore,
            NullValueHandling = NullValueHandling.Ignore
        };

        public JsonSerializer(params Type[] knownTypes)
        {
            if (knownTypes != null && knownTypes.Length == 0)
            {
                knownTypes = null;
            }

            _knownTypes = knownTypes ?? _knownTypes;
        }

        protected JsonSerializer(ILogger logger, params Type[] knownTypes) : this(knownTypes)
        {
            _logger = logger;

            foreach (var type in _knownTypes)
            {
                _logger?.LogDebug(Messages.RegisteringKnownType, type);
            }
        }

        public JsonSerializer(ILogger<JsonSerializer> logger, params Type[] knownTypes) : this(logger as ILogger, knownTypes) { }

        public virtual void Serialize<T>(Stream output, T graph)
        {
            _logger?.LogTrace(Messages.SerializingGraph, typeof(T));

            using (var streamWriter = new StreamWriter(output, Encoding.UTF8))
            {
                Serialize(new JsonTextWriter(streamWriter), graph);
            }
        }

        public virtual T Deserialize<T>(Stream input)
        {
            _logger?.LogTrace(Messages.DeserializingStream, typeof(T));

            using (var streamReader = new StreamReader(input, Encoding.UTF8))
            {
                return Deserialize<T>(new JsonTextReader(streamReader));
            }
        }

        protected virtual void Serialize(JsonWriter writer, object graph)
        {
            using (writer)
            {
                GetSerializer(graph.GetType()).Serialize(writer, graph);
            }
        }

        protected virtual T Deserialize<T>(JsonReader reader)
        {
            var type = typeof(T);

            using (reader)
            {
                return (T)GetSerializer(type).Deserialize(reader, type);
            }
        }

        protected virtual Newtonsoft.Json.JsonSerializer GetSerializer(Type typeToSerialize)
        {
            if (_knownTypes.Contains(typeToSerialize))
            {
                _logger?.LogTrace(Messages.UsingUntypedSerializer, typeToSerialize);
                return _untypedSerializer;
            }

            _logger?.LogTrace(Messages.UsingTypedSerializer, typeToSerialize);
            return _typedSerializer;
        }
    }
}