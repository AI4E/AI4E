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
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Threading;
using AI4E.Internal;
using AI4E.Utils;
using AI4E.Utils.Messaging.Primitives;
using Newtonsoft.Json;

#if MODULE
using AI4E.Messaging;
using AI4E.Messaging.Serialization;

namespace AI4E.AspNetCore.Components.Modularity
#else
namespace AI4E.Messaging.Serialization
#endif
{
#if MODULE
    internal
#else
    public 
#endif
        sealed class MessageSerializer : IMessageSerializer, IDisposable
    {
        private readonly ITypeResolver _typeResolver;
        private readonly ThreadLocal<JsonSerializer> _serializer;

        public MessageSerializer(ITypeResolver typeResolver)
        {
            if (typeResolver is null)
                throw new ArgumentNullException(nameof(typeResolver));

            _typeResolver = typeResolver;
            _serializer = new ThreadLocal<JsonSerializer>(BuildSerializer, trackAllValues: false);
        }

        /// <inheritdoc/>
        public Message Serialize(IDispatchResult dispatchResult)
        {
            if (dispatchResult is null)
                throw new ArgumentNullException(nameof(dispatchResult));

            return Serialize(typeof(IDispatchResult), dispatchResult);
        }

        /// <inheritdoc/>
        public Message Serialize(DispatchDataDictionary dispatchData)
        {
            if (dispatchData is null)
                throw new ArgumentNullException(nameof(dispatchData));

            return Serialize(typeof(DispatchDataDictionary), dispatchData);
        }

        private Message Serialize(Type type, object dispatchResult)
        {
            var messageBuilder = new MessageBuilder();

            using (var frameStream = messageBuilder.PushFrame().OpenStream())
            using (var writer = new StreamWriter(frameStream))
            using (var jsonWriter = new JsonTextWriter(writer))
            {
                var serializer = GetSerializer();
                serializer.Serialize(jsonWriter, dispatchResult, type);
            }

            return messageBuilder.BuildMessage();
        }

        /// <inheritdoc/>
        public bool TryDeserialize(Message message, [NotNullWhen(true)] out IDispatchResult? dispatchResult)
        {
            dispatchResult = Deserialize(message, typeof(IDispatchResult)) as IDispatchResult;
            return dispatchResult != null;
        }

        /// <inheritdoc/>
        public bool TryDeserialize(Message message, [NotNullWhen(true)] out DispatchDataDictionary? dispatchData)
        {
            dispatchData = Deserialize(message, typeof(DispatchDataDictionary)) as DispatchDataDictionary;
            return dispatchData != null;
        }

        private object? Deserialize(Message message, Type type)
        {
            message.PopFrame(out var frame);

            using var frameStream = frame.OpenStream();
            using var reader = new StreamReader(frameStream);
            using var jsonReader = new JsonTextReader(reader);

            var serializer = GetSerializer();

            return serializer.Deserialize(jsonReader, type);
        }

        private JsonSerializer GetSerializer()
        {
            var result = _serializer.Value!;
            Debug.Assert(result != null);
            return result!;
        }

        private JsonSerializer BuildSerializer()
        {
            var result = new JsonSerializer
            {
                ContractResolver = ContractResolver.Instance,
                SerializationBinder = new SerializationBinder(_typeResolver),
                TypeNameHandling = TypeNameHandling.Auto,
            };

            result.Converters.Add(new TypeConverter());
            result.Converters.Add(new DispatchDataDictionaryConverter());

            return result;
        }

        public void Dispose()
        {
            _serializer.Dispose();
        }
    }
}
