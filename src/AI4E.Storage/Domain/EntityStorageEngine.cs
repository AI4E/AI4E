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
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Storage.Domain
{
    public sealed partial class EntityStorageEngine : IEntityStorageEngine
    {
        #region Fields

        private readonly IStreamStore _streamStore;
        private readonly IEntityPropertyAccessor _entityPropertyAccessor;
        private readonly JsonDiffPatch _differ;
        private readonly JsonSerializer _jsonSerializer;
        private readonly Dictionary<(string bucket, string id, long requestedRevision), (object entity, long revision)> _lookup;
        private bool _isDisposed;

        #endregion

        #region C'tor

        public EntityStorageEngine(IStreamStore streamStore,
                                   IEntityPropertyAccessor entityPropertyAccessor,
                                   ISerializerSettingsResolver serializerSettingsResolver)
        {
            if (streamStore == null)
                throw new ArgumentNullException(nameof(streamStore));

            if (entityPropertyAccessor == null)
                throw new ArgumentNullException(nameof(entityPropertyAccessor));

            if (serializerSettingsResolver == null)
                throw new ArgumentNullException(nameof(serializerSettingsResolver));

            _streamStore = streamStore;
            _entityPropertyAccessor = entityPropertyAccessor;
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
            var bucketId = GetBucketId(entityType);

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

            var bucketId = GetBucketId(entityType);

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

            var bucketId = GetBucketId(entityType);
            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);

            var (concurrencyToken, events) = GetEntityProperties(entityType, entity);
            var commitBody = BuildCommitBody(entity, stream);

            void HeaderGenerator(IDictionary<string, object> headers) { }

            var success = await stream.TryCommitAsync(concurrencyToken,
                                                      events,
                                                      commitBody,
                                                      HeaderGenerator,
                                                      cancellation);

            if (!success)
            {
                // The stream did already update because of the concurrency conflict,
                // but we need to reload the entity and put it in the cache.
                entity = Deserialize(entityType, stream);
            }
            else
            {
                _entityPropertyAccessor.SetConcurrencyToken(entityType, entity, stream.ConcurrencyToken);
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

            var bucketId = GetBucketId(entityType);
            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);

            var (concurrencyToken, events) = GetEntityProperties(entityType, entity);
            var commitBody = BuildCommitBody(entity: null, stream);

            void HeaderGenerator(IDictionary<string, object> headers) { }

            var success = await stream.TryCommitAsync(concurrencyToken,
                                                     events,
                                                     commitBody,
                                                     HeaderGenerator,
                                                     cancellation);

            if (!success)
            {
                // The stream did already update because of the concurrency conflict,
                // but we need to reload the entity and put it in the cache.
                entity = Deserialize(entityType, stream);
            }
            else
            {
                entity = null;
            }

            _lookup[(bucketId, streamId, requestedRevision: default)] = (entity, revision: stream.StreamRevision);

            return success;
        }

        #endregion

        private (string concurrencyToken, IEnumerable<EventMessage> events) GetEntityProperties(Type entityType, object entity)
        {
            var concurrencyToken = _entityPropertyAccessor.GetConcurrencyToken(entityType, entity);
            var events = _entityPropertyAccessor.GetUncommittedEvents(entityType, entity).Select(p => new EventMessage { Body = Serialize(p) });

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

            JToken baseToken;
            if (stream.Snapshot == null)
            {
                baseToken = StreamRoot;
            }
            else
            {
                var snapshotPayload = stream.Snapshot.Payload as byte[];

                baseToken = JToken.Parse(CompressionHelper.Unzip(snapshotPayload));
            }

            foreach (var commit in stream.Commits)
            {
                var commitPayload = commit.Body as byte[];

                baseToken = _differ.Patch(baseToken, JToken.Parse(CompressionHelper.Unzip(commitPayload)));
            }

            return baseToken;
        }

        private object Deserialize(Type entityType, IStream stream)
        {
            // This is an empty stream.
            if (stream.StreamRevision == default)
                return null;

            var serializedEntity = default(JToken);

            if (stream.Snapshot == null)
            {
                serializedEntity = StreamRoot;
            }
            else
            {
                serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
            }

            foreach (var commit in stream.Commits)
            {
                serializedEntity = _differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
            }

            var result = serializedEntity.ToObject(entityType, _jsonSerializer);

            if (result != null)
            {
                _entityPropertyAccessor.SetConcurrencyToken(entityType, result, stream.ConcurrencyToken);
                _entityPropertyAccessor.SetRevision(entityType, result, stream.StreamRevision);
                _entityPropertyAccessor.CommitEvents(entityType, result);
            }

            return result;
        }

        private object CachedDeserialize(Type entityType, long revision, IStream stream)
        {
            var bucketId = GetBucketId(entityType);

            if (_lookup.TryGetValue((bucketId, stream.StreamId, revision), out var cachedResult))
            {
                return cachedResult;
            }

            Assert(revision != default, stream.StreamRevision == revision);
            var entity = Deserialize(entityType, stream);

            _lookup[(bucketId, stream.StreamId, revision)] = (entity, revision: stream.StreamRevision);

            return entity;
        }

        private async ValueTask<object> CachedDeserializeAsync(Type entityType,
                                                               string id,
                                                               long revision,
                                                               CancellationToken cancellation)
        {
            var bucketId = GetBucketId(entityType);

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

        private static string GetBucketId(Type entityType)
        {
            return entityType.ToString();
        }

        private static Type GetTypeFromBucket(string bucketId)
        {
            return TypeLoadHelper.LoadTypeFromUnqualifiedName(bucketId);
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
