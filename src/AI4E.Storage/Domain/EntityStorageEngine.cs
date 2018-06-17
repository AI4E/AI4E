/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        EntityStorageEngine.cs 
 * Types:           (1) AI4E.Storage.Domain.EntityStorageEngine
 *                  (2) AI4E.Storage.Domain.EntityStorageEngine.SnapshotProcessor
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   17.06.2018 
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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Storage.Internal;
using JsonDiffPatchDotNet;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace AI4E.Storage.Domain
{
    public sealed partial class EntityStorageEngine : IEntityStorageEngine
    {
        #region Fields

        private readonly IStreamStore _streamStore;
        private readonly IEntityStoragePropertyManager _entityAccessor; // TODO: Rename
        private readonly JsonDiffPatch _differ;
        private readonly JsonSerializer _jsonSerializer;
        private readonly Dictionary<(string bucket, string id, long revision), object> _lookup;
        private bool _isDisposed;

        #endregion

        #region C'tor

        public EntityStorageEngine(IStreamStore streamStore,
                                   IEntityStoragePropertyManager entityAccessor, // TODO: Rename
                                   ISerializerSettingsResolver serializerSettingsResolver)
        {
            if (streamStore == null)
                throw new ArgumentNullException(nameof(streamStore));

            if (entityAccessor == null)
                throw new ArgumentNullException(nameof(entityAccessor));

            if (serializerSettingsResolver == null)
                throw new ArgumentNullException(nameof(serializerSettingsResolver));

            _streamStore = streamStore;
            _entityAccessor = entityAccessor;
            _differ = new JsonDiffPatch();
            _jsonSerializer = JsonSerializer.Create(serializerSettingsResolver.ResolveSettings(this));

            _lookup = new Dictionary<(string bucket, string id, long revision), object>();
        }

        #endregion

        public IEnumerable<(Type type, string id, long revision, object entity)> CachedEntries
        {
            get
            {
                foreach (var entry in _lookup)
                {
                    yield return (type: GetTypeFromBucket(entry.Key.bucket),
                                  entry.Key.id,
                                  entry.Key.revision,
                                  entity: entry.Value);
                }
            }
        }

        private static JToken StreamRoot => JToken.Parse("{}");

        #region IEntityStorageEngine

        public ValueTask<object> GetByIdAsync(Type entityType, string id, CancellationToken cancellation)
        {
            return GetByIdAsync(entityType, id, revision: default, cancellation: cancellation);
        }

        public async ValueTask<object> GetByIdAsync(Type entityType, string id, long revision, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            var bucketId = GetBucketId(entityType);

            if (_lookup.TryGetValue((bucketId, id, revision), out var cachedResult))
                return cachedResult;


            var streamId = id;
            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, revision, cancellation);

            var result = Deserialize(entityType, stream);

            _lookup[(bucketId, id, revision)] = result;

            return result;
        }

        public IAsyncEnumerable<object> GetAllAsync(Type entityType, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            var bucketId = GetBucketId(entityType);

            return _streamStore.OpenAllAsync(bucketId, cancellation).Select(stream => CachedDeserialize(entityType, revision: default, stream));
        }

        public IAsyncEnumerable<object> GetAllAsync(CancellationToken cancellation)
        {
            return _streamStore.OpenAllAsync(cancellation).Select(stream => CachedDeserialize(GetTypeFromBucket(stream.BucketId), revision: default, stream));
        }

        public async Task StoreAsync(Type entityType, object entity, string id, CancellationToken cancellation)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            if (!entityType.IsAssignableFrom(entity.GetType()))
                throw new ArgumentException($"The specified entity must be of type '{entityType.FullName}' or a derived type.", nameof(entity));

            var bucketId = GetBucketId(entityType);
            var streamId = id;
            var concurrencyToken = _entityAccessor.GetConcurrencyToken(entity);

            var stream = await _streamStore.OpenStreamAsync(bucketId, streamId, throwIfNotFound: false, cancellation);
            var baseToken = default(JToken);

            if (stream.Snapshot == null)
            {
                baseToken = StreamRoot;
            }
            else
            {
                baseToken = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
            }

            foreach (var commit in stream.Commits)
            {
                baseToken = _differ.Patch(baseToken, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
            }

            var serializedEntity = JToken.FromObject(entity, _jsonSerializer);
            var diff = _differ.Diff(baseToken, serializedEntity);

            var events = _entityAccessor.GetUncommittedEvents(entity).Select(p => new EventMessage { Body = p });

            await stream.CommitAsync(concurrencyToken,
                                     events,
                                     CompressionHelper.Zip(diff.ToString()),
                                     headers => { },
                                     cancellation);

            _entityAccessor.SetConcurrencyToken(entity, stream.ConcurrencyToken);
            _entityAccessor.SetRevision(entity, stream.StreamRevision);
            _entityAccessor.CommitEvents(entity);

            _lookup[(bucketId, id, revision: default)] = entity;
        }

        public Task DeleteAsync(Type entityType, string id, CancellationToken cancellation)
        {
            if (entityType == null)
                throw new ArgumentNullException(nameof(entityType));

            if (id == null)
                throw new ArgumentNullException(nameof(id));

            if (entityType.IsValueType)
                throw new ArgumentException("The argument must specify a reference type.", nameof(entityType));

            throw new NotSupportedException(); // TODO
        }

        #endregion

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

            _entityAccessor.SetConcurrencyToken(result, stream.ConcurrencyToken);
            _entityAccessor.SetRevision(result, stream.StreamRevision);
            _entityAccessor.CommitEvents(result);

            return result;
        }

        private object CachedDeserialize(Type entityType, long revision, IStream stream)
        {
            var bucketId = GetBucketId(entityType);

            if (_lookup.TryGetValue((bucketId, stream.StreamId, revision), out var cachedResult))
            {
                return cachedResult;
            }

            var result = Deserialize(entityType, stream);

            _lookup[(bucketId, stream.StreamId, revision)] = result;

            return result;
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

        #region Helpers

        private static string GetBucketId(Type entityType)
        {
            return entityType.ToString();
        }

        private static Type GetTypeFromBucket(string bucketId)
        {
            return TypeLoadHelper.LoadTypeFromUnqualifiedName(bucketId);
        }

        #endregion 
    }
}
