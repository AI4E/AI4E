/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        SerializationExtensions.cs 
 * Types:           (1) AI4E.Serialization.SerializationExtensions
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

using System.IO;

namespace AI4E.Serialization
{
    /// <summary>
    /// Implements extension methods that make call to the serialization infrastructure more simple.
    /// </summary>
    public static class SerializationExtensions
    {
        /// <summary>
        /// Serializes the object provided.
        /// </summary>
        /// <typeparam name="T">The type of object to be serialized</typeparam>
        /// <param name="serializer">The serializer to use.</param>
        /// <param name="value">The object graph to be serialized.</param>
        /// <returns>A serialized representation of the object graph provided.</returns>
        public static byte[] Serialize<T>(this ISerializer serializer, T value)
        {
            using (var stream = new MemoryStream())
            {
                serializer.Serialize(stream, value);
                return stream.ToArray();
            }
        }

        /// <summary>
        /// Deserializes the array of bytes provided.
        /// </summary>
        /// <typeparam name="T">The type of object to be deserialized.</typeparam>
        /// <param name="serializer">The serializer to use.</param>
        /// <param name="serialized">The serialized array of bytes.</param>
        /// <returns>The reconstituted object, if any.</returns>
        public static T Deserialize<T>(this ISerializer serializer, byte[] serialized)
        {
            serialized = serialized ?? new byte[] { };

            if (serialized.Length == 0)
                return default;

            using (var stream = new MemoryStream(serialized))
            {
                return serializer.Deserialize<T>(stream);
            }

        }
    }
}