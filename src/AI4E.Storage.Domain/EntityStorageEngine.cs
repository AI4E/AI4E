/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EntityStorageEngine.cs 
 * Types:           (1) AI4E.Storage.Domain.EntityStorageEngine
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   23.06.2018 
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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Utils;
using JsonDiffPatchDotNet;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Diagnostics.Debug;
using static AI4E.Utils.DebugEx;

namespace AI4E.Storage.Domain
{
    public sealed partial class EntityStorageEngine : IEntityStorageEngine
    {
        internal const string ConcurrencyTokenHeaderKey = "ConcurrencyToken";


        #region Fields

        private readonly IStreamStore _streamStore;
        private readonly IEntityPropertyAccessor _entityPropertyAccessor;
        private readonly ILogger<EntityStorageEngine> _logger;

        private readonly DomainStorageOptions _options;
        private readonly JsonDiffPatch _differ;
        private readonly JsonSerializer _jsonSerializer;
        private readonly Dictionary<(string bucket, string id, long requestedRevision), (object entity, long revision)> _lookup;
        private bool _isDisposed;

        #endregion

        #region C'tor

        public EntityStorageEngine(IStreamStore streamStore,
                                   IEntityPropertyAccessor entityPropertyAccessor,
                                   ISerializerSettingsResolver serializerSettingsResolver,
                                   IOptions<DomainStorageOptions> optionsAccessor,
                                   ILogger<EntityStorageEngine> logger = null)
        {
            if (streamStore == null)
                throw new ArgumentNullException(nameof(streamStore));

            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            if (serializerSettingsResolver == null)
                throw new ArgumentNullException(nameof(serializerSettingsResolver));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _streamStore = streamStore;
            _entityPropertyAccessor = entityPropertyAccessor;
            _logger = logger;
            _options = optionsAccessor.Value ?? new DomainStorageOptions();

            _differ = new JsonDiffPatch();
            _jsonSerializer = JsonSerializer.Create(serializerSettingsResolver.ResolveSettings(this));

            _lookup = new Dictionary<(string bucket, string id, long requestedRevision), (object entity, long revision)>();
        }

        #endregion

        public IEnumerable<(Type type, string id, long revision, object entity)> LoadedEntries =>
            _lookup.Where(p => p.Key.requestedRevision == default)
                   .Select(p => (type: GetTypeFromBucket(p.Key.bucket), p.Key.id, p.Value.revision, p.Value.entity));

        private static JToken StreamRoot => JToken.Parse("null");

        #region IEntityStorageEngine

        public ValueTask<object> GetByIdAsync(Type entityType, string id, CancellationToken cancellation)
        {
            return GetByIdAsync(entityType, id, revision: default, cancellation: cancellation);
        }

        public ValueTask<object> GetByIdAsync(Type entityType, string id, long revision, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            if (id == null)
            {
                return new ValueTask<object>(result: null);
            }

            return CachedDeserializeAsync(entityType, id, revision, cancellation);
        }

        public async ValueTask<(object entity, long revision)> LoadEntityAsync(Type entityType, string id, CancellationToken cancellation = default)
        {
            var bucketId = GetBucket(entityType);

            if (!_lookup.TryGetValue((bucketId, id, requestedRevision: default), out var result))
            {
                var stream = await _streamStore.OpenStreamAsync(bucketId, id, revision: default, cancellation);
                var entity = Deserialize(entityType, stream);
                result = (entity, revision: stream.StreamRevision);

                _lookup[(bucketId, stream.StreamId, requestedRevision: default)] = result;
            }

            return result;
        }

        public IAsyncEnumerable<object> GetAllAsync(Type entityType, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            var bucketId = GetBucket(entityType);

            return _streamStore.OpenAllAsync(bucketId, cancellation)
                               .Select(stream => CachedDeserialize(entityType, revision: default, stream))
                               .Where(p => p != null); // TODO: Add a deleted marker to the stream to already filter the streams before deserializing the content and checking for null afterwards.
        }

        public IAsyncEnumerable<object> GetAllAsync(CancellationToken cancellation)
        {
            return _streamStore.OpenAllAsync(cancellation)
                               .Select(stream => CachedDeserialize(GetTypeFromBucket(stream.BucketId), revision: default, stream))
                               .Where(p => p != null); // TODO: Add a deleted marker to the stream to already filter the streams before deserializing the content and checking for null afterwards.;
        }

        public Task<bool> TryStoreAsync(Type entityType, object entity, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_entityPropertyAccessor.TryGetId(entityType, entity, out var id))
            {
                throw new ArgumentException("Unable to determine the id of the specified entity.", nameof(entity));
            }

            Assert(id != null);

            return InternalTryStoreAsync(entityType, entity, id, cancellation);
        }

        public Task<bool> TryStoreAsync(Type entityType, object entity, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            return InternalTryStoreAsync(entityType, entity, id, cancellation);
        }

        private async Task<bool> InternalTryStoreAsync(Type entityType, object entity, string id, CancellationToken cancellation)
        {
            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or a derived type.", nameof(entity));

            var bucketId = GetBucket(entityType);
            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);

            var (concurrencyToken, events) = GetEntityProperties(entityType, entity);

            var success = await CheckConcurrencyAsync(stream, concurrencyToken, cancellation);

            if (success)
            {
                var commitBody = BuildCommitBody(entity, stream);
                var newConcurrencyToken = SGuid.NewGuid().ToString();

                void GenerateHeaders(IDictionary<string, object> headers)
                {
                    headers[ConcurrencyTokenHeaderKey] = newConcurrencyToken;
                }

                if (!await stream.TryCommitAsync(events, commitBody, GenerateHeaders, cancellation))
                {
                    success = false;
                }
            }

            if (!success)
            {
                // The stream did already update because of the concurrency conflict,
                // but we need to reload the entity and put it in the cache.
                entity = Deserialize(entityType, stream);
            }
            else
            {
                _entityPropertyAccessor.SetConcurrencyToken(entityType, entity, (string)stream.Headers[ConcurrencyTokenHeaderKey]);
                _entityPropertyAccessor.SetRevision(entityType, entity, stream.StreamRevision);
                _entityPropertyAccessor.CommitEvents(entityType, entity);
            }

            _lookup[(bucketId, streamId, requestedRevision: default)] = (entity, revision: stream.StreamRevision);

            return success;
        }

        public Task<bool> TryDeleteAsync(Type entityType, object entity, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (!_entityPropertyAccessor.TryGetId(entityType, entity, out var id))
            {
                throw new ArgumentException("Unable to determine the id of the specified entity.", nameof(entity));
            }

            Assert(id != null);

            return InternalTryDeleteAsync(entityType, entity, id, cancellation);
        }

        public Task<bool> TryDeleteAsync(Type entityType, object entity, string id, CancellationToken cancellation = default)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            return InternalTryDeleteAsync(entityType, entity, id, cancellation);
        }

        private async Task<bool> InternalTryDeleteAsync(Type entityType, object entity, string id, CancellationToken cancellation)
        {
            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or a derived type.", nameof(entity));

            var bucketId = GetBucket(entityType);
            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);

            var (concurrencyToken, events) = GetEntityProperties(entityType, entity);

            var success = await CheckConcurrencyAsync(stream, concurrencyToken, cancellation);

            if (success)
            {
                var commitBody = BuildCommitBody(entity: null, stream);
                var newConcurrencyToken = SGuid.NewGuid().ToString();

                void GenerateHeaders(IDictionary<string, object> headers)
                {
                    headers[ConcurrencyTokenHeaderKey] = newConcurrencyToken;
                }

                if (await stream.TryCommitAsync(events, commitBody, GenerateHeaders, cancellation))
                {
                    entity = null;
                }
                else
                {
                    success = false;
                }
            }

            if (!success)
            {
                // The stream did already update because of the concurrency conflict,
                // but we need to reload the entity and put it in the cache.
                entity = Deserialize(entityType, stream);
            }

            _lookup[(bucketId, streamId, requestedRevision: default)] = (entity, revision: stream.StreamRevision);

            return success;
        }

        private async Task<bool> CheckConcurrencyAsync(IStream stream, string concurrencyToken, CancellationToken cancellation)
        {
            Assert(!stream.IsReadOnly);

            if (stream.StreamRevision == 0)
                return true;

            if (concurrencyToken == (string)stream.Headers[ConcurrencyTokenHeaderKey])
                return true;

            if (stream.Commits.TakeAllButLast().Any(p => (string)p.Headers[ConcurrencyTokenHeaderKey] == concurrencyToken))
            {
                return false;
            }

            if (await stream.UpdateAsync(cancellation) && concurrencyToken == (string)stream.Headers[ConcurrencyTokenHeaderKey])
            {
                return true;
            }

            // Either a concurrency token of a commit that is not present because of a snapshot or an unknown token 
            return false;
        }

        #endregion

        private (string concurrencyToken, IEnumerable<EventMessage> events) GetEntityProperties(Type entityType, object entity)
        {
            var concurrencyToken = _entityPropertyAccessor.GetConcurrencyToken(entityType, entity);
            var events = _entityPropertyAccessor.GetUncommittedEvents(entityType, entity).Select(p => new EventMessage(Serialize(p)));

            return (concurrencyToken, events);
        }

        private byte[] Serialize(object obj)
        {
            if (obj == null)
                return null;

            var jsonBuilder = new StringBuilder();

            using (var textWriter = new StringWriter(jsonBuilder))
            {
                _jsonSerializer.Serialize(textWriter, obj, typeof(object));
            }

            return CompressionHelper.Zip(jsonBuilder.ToString());
        }

        private byte[] BuildCommitBody(object entity, IStream stream)
        {
            var baseToken = GetBaseToken(stream);
            var serializedEntity = GetSerializedEntity(entity);
            var diff = _differ.Diff(baseToken, serializedEntity);

            if (diff == null)
            {
                return Array.Empty<byte>();
            }

            return CompressionHelper.Zip(diff.ToString());
        }

        private JToken GetSerializedEntity(object entity)
        {
            if (entity == null)
            {
                return StreamRoot;
            }

            return JToken.FromObject(entity, _jsonSerializer);
        }

        private JToken GetBaseToken(IStream stream)
        {
            Assert(stream != null);

            JToken token = null;
            if (stream.Snapshot == null)
            {
                token = StreamRoot;
            }
            else if (stream.Snapshot.Payload is byte[] snapshotPayload && snapshotPayload.Length > 0)
            {
                token = JToken.Parse(CompressionHelper.Unzip(snapshotPayload));
            }

            foreach (var commit in stream.Commits)
            {
                if (commit.Body is byte[] commitPayload && commitPayload.Length > 0)
                {
                    token = _differ.Patch(token, JToken.Parse(CompressionHelper.Unzip(commitPayload)));
                }
            }

            return token;
        }

        private object Deserialize(Type entityType, IStream stream)
        {
            // This is an empty stream.
            if (stream.StreamRevision == default)
                return null;

            var token = GetBaseToken(stream);
            var result = token.ToObject(entityType, _jsonSerializer);

            if (result != null)
            {
                _entityPropertyAccessor.SetConcurrencyToken(entityType, result, (string)stream.Headers[ConcurrencyTokenHeaderKey]);
                _entityPropertyAccessor.SetRevision(entityType, result, stream.StreamRevision);
                _entityPropertyAccessor.CommitEvents(entityType, result);
            }

            return result;
        }

        private object CachedDeserialize(Type entityType, long revision, IStream stream)
        {
            var bucketId = GetBucket(entityType);

            if (_lookup.TryGetValue((bucketId, stream.StreamId, revision), out var cachedResult))
            {
                var result = cachedResult.entity;
                Assert(result == null || entityType.IsAssignableFrom(result.GetType()));
                return result;
            }

            Assert(revision != default, stream.StreamRevision == revision);
            var entity = Deserialize(entityType, stream);
            Assert(entity == null || entityType.IsAssignableFrom(entity.GetType()));

            _lookup[(bucketId, stream.StreamId, revision)] = (entity, revision: stream.StreamRevision);

            return entity;
        }

        private async ValueTask<object> CachedDeserializeAsync(Type entityType,
                                                               string id,
                                                               long revision,
                                                               CancellationToken cancellation)
        {
            var bucketId = GetBucket(entityType);

            if (_lookup.TryGetValue((bucketId, id, revision), out var cachedResult))
            {
                return cachedResult.entity;
            }

            var stream = await _streamStore.OpenStreamAsync(bucketId, id, revision, cancellation);

            Assert(revision != default, stream.StreamRevision == revision);
            var entity = Deserialize(entityType, stream);

            _lookup[(bucketId, stream.StreamId, revision)] = (entity, revision: stream.StreamRevision);

            return entity;
        }

        private const string _separatorString = "->";

        private string GetBucket(Type entityType)
        {
            var stringifiedType = entityType.ToString();

            if (string.IsNullOrWhiteSpace(_options.Scope))
            {
                return stringifiedType;
            }

            var resultsBuilder = new StringBuilder(stringifiedType.Length +
                                                   _options.Scope.Length +
                                                   EscapeHelper.CountCharsToEscape(stringifiedType) +
                                                   EscapeHelper.CountCharsToEscape(_options.Scope) +
                                                   2);

            resultsBuilder.Append(stringifiedType);
            EscapeHelper.Escape(resultsBuilder, 0);
            var sepIndex = resultsBuilder.Length;
            resultsBuilder.Append(' ');
            resultsBuilder.Append(' ');
            resultsBuilder.Append(_options.Scope);
            EscapeHelper.Escape(resultsBuilder, sepIndex + 2);
            // We need to ensure that the created entry is unique.
            resultsBuilder[sepIndex] = _separatorString[0];
            resultsBuilder[sepIndex + 1] = _separatorString[1];
            return resultsBuilder.ToString();
        }

        internal static bool IsInScope(string bucket, string scope, out string typeName)
        {
            var index = bucket.IndexOf(_separatorString);

            if (string.IsNullOrWhiteSpace(scope))
            {
                if (index == -1)
                {
                    typeName = bucket;
                    return true;
                }
                else
                {
                    typeName = null;
                    return false;
                }
            }

            if (index == -1)
            {
                typeName = null;
                return false;
            }

            var scopeBuilder = new StringBuilder(bucket, index + 2, bucket.Length - index - 2, bucket.Length - index - 2);
            EscapeHelper.Unescape(scopeBuilder, 0);

            if (!scope.Equals(scopeBuilder.ToString()))
            {
                typeName = null;
                return false;
            }

            var typeNameBuilder = new StringBuilder(bucket, 0, index, index);
            EscapeHelper.Unescape(typeNameBuilder, 0);

            typeName = typeNameBuilder.ToString();
            return true;
        }

        private Type GetTypeFromBucket(string bucketId)
        {
            return IsInScope(bucketId, _options.Scope, out var typeName) ? TypeLoadHelper.LoadTypeFromUnqualifiedName(typeName) : null;
        }

        public void Dispose()
        {
            if (_isDisposed)
            {
                return;
            }

            _isDisposed = true;
            _streamStore.Dispose();
        }
    }
}
