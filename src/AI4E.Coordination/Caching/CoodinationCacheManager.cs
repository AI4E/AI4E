using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Locking;
using AI4E.Coordination.Session;
using AI4E.Coordination.Storage;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.ObjectPool;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination.Caching
{
    public sealed class CoodinationCacheManager : ICoordinationCacheManager
    {
        private readonly ISessionOwner _sessionOwner;
        private readonly ICoordinationStorage _storage;
        private readonly ICoordinationLockManager _lockManager;
        private readonly IInvalidationCallbackDirectory _invalidationCallbackDirectory;

        private readonly ConcurrentDictionary<string, ICacheEntry> _cache = new ConcurrentDictionary<string, ICacheEntry>();

        public CoodinationCacheManager(
            ISessionOwner sessionOwner,
            ICoordinationStorage storage,
            ICoordinationLockManager lockManager,
            IInvalidationCallbackDirectory invalidationCallbackDirectory)
        {
            if (sessionOwner == null)
                throw new ArgumentNullException(nameof(sessionOwner));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (lockManager == null)
                throw new ArgumentNullException(nameof(lockManager));

            if (invalidationCallbackDirectory == null)
                throw new ArgumentNullException(nameof(invalidationCallbackDirectory));

            _sessionOwner = sessionOwner;
            _storage = storage;
            _lockManager = lockManager;
            _invalidationCallbackDirectory = invalidationCallbackDirectory;
        }

        public async ValueTask<ICacheEntry> GetCacheEntryAsync(string key, CancellationToken cancellation)
        {
            if (_cache.TryGetValue(key, out var result))
            {
                return result;
            }

            return await SlowGetChaceEntryAsync(key);
        }

        private async ValueTask<ICacheEntry> SlowGetChaceEntryAsync(string key)
        {
            var session = await _sessionOwner.GetSessionIdentifierAsync(cancellation: default);
            var cacheEntry = new CacheEntry(key, this, session);
#pragma warning disable IDE0039
            Func<CancellationToken, ValueTask> invalidationCallback = cacheEntry.InvalidateAsync;
#pragma warning restore IDE0039
            _invalidationCallbackDirectory.Register(key, invalidationCallback);

            try
            {
                var result = _cache.GetOrAdd(key, cacheEntry);
                if (result != cacheEntry)
                {
                    _invalidationCallbackDirectory.Unregister(key, invalidationCallback);
                }

                return result;
            }
            catch
            {
                _invalidationCallbackDirectory.Unregister(key, invalidationCallback);

                throw;
            }
        }

        private sealed class CacheEntry : ICacheEntry
        {
            /// <summary>
            /// Protects the cache entry from becoming inconsistent (out of sync with the global state)
            /// </summary>
            private readonly AsyncReaderWriterLock _mutex = new AsyncReaderWriterLock();

            /// <summary>
            /// Prevents concurrent write attempts from a single session.
            /// </summary>
            private readonly AsyncLock _lockMutex = new AsyncLock();

            private readonly CoodinationCacheManager _cacheManager;
            private readonly SessionIdentifier _session;
            private volatile IStoredEntry _entry;

            public CacheEntry(string key, CoodinationCacheManager cacheManager, SessionIdentifier session)
            {
                Key = key;
                _cacheManager = cacheManager;
                _session = session;
            }

            public CacheEntry(IStoredEntry entry, CoodinationCacheManager cacheManager, SessionIdentifier session)
            {
                Key = entry.Key;
                _entry = entry;
                _cacheManager = cacheManager;
                _session = session;
            }

            public string Key { get; }

            private bool IsLockedEntry(IStoredEntry entry)
            {
                return entry != null && entry.ReadLocks.Contains(_session);
            }

            public bool TryGetValue(out CacheEntryValue value)
            {
                // Volatile read op.
                var entry = _entry;

                if (!IsLockedEntry(entry))
                {
                    value = default;
                    return false;
                }

                value = entry.IsMarkedAsDeleted ? default : new CacheEntryValue(entry.Value);
                return true;
            }

            public async ValueTask<CacheEntryValue> GetValueAsync(CancellationToken cancellation)
            {
                var entry = await GetEntryAsync(cancellation);

                if (entry == null || entry.IsMarkedAsDeleted)
                {
                    return default;
                }

                return new CacheEntryValue(entry.Value);
            }

            public ValueTask<LockedEntry> LockAsync(LockType lockType, CancellationToken cancellation)
            {
                if (lockType == LockType.Exclusive)
                {
                    return ExclusiveLockAsync(cancellation);
                }

                return SharedLockAsync(cancellation);
            }

            private async ValueTask<LockedEntry> SharedLockAsync(CancellationToken cancellation)
            {
                var entry = await GetEntryAsync(cancellation);
                var lockReleaser = await _mutex.ReaderLockAsync(cancellation);

                try
                {
                    while (entry != null && !IsLockedEntry(entry))
                    {
                        lockReleaser.Dispose();
                        entry = await GetEntryAsync(cancellation);
                        lockReleaser = await _mutex.ReaderLockAsync(cancellation);
                    }

                    ValueTask UnlockAsync()
                    {
                        lockReleaser.Dispose();
                        return default;
                    }

                    var isExisting = entry != null && !entry.IsMarkedAsDeleted;
                    var value = isExisting ? entry.Value : ReadOnlyMemory<byte>.Empty;

                    var lockReleaserSource = LockedEntrySource.Allocate(value, isExisting, UnlockAsync);

                    return new LockedEntry(Key, lockReleaserSource, LockType.Shared);
                }
                catch
                {
                    lockReleaser.Dispose();
                    throw;
                }
            }

            private async ValueTask<LockedEntry> ExclusiveLockAsync(CancellationToken cancellation)
            {
                var mutexUnlocker = await _lockMutex.LockAsync(cancellation);

                try
                {
                    IStoredEntry entry;
                    using (await _mutex.WriterLockAsync(cancellation))
                    {
                        entry = await _cacheManager._lockManager.AcquireWriteLockAsync(Key, cancellation); // We own the write-lock NOW

                        Assert(entry != null);
                        Assert(entry.WriteLock == _session);
                        Assert(entry.IsMarkedAsDeleted || IsLockedEntry(entry));

                        Assert(_entry == null || entry.StorageVersion >= _entry.StorageVersion);
                        _entry = entry;
                        Assert(_entry.IsMarkedAsDeleted || IsLockedEntry(_entry));
                    }

                    async ValueTask UnlockAsync(bool modified, ReadOnlyMemory<byte> value, bool deleted)
                    {
                        try
                        {
                            IStoredEntry desired = null;

                            if (modified)
                            {
                                var builder = entry.ToBuilder(_session);

                                if (deleted)
                                {
                                    builder.MarkAsDeleted();
                                }
                                else if (entry.IsMarkedAsDeleted)
                                {
                                    builder.Create(value);
                                }
                                else
                                {
                                    builder.SetValue(value);
                                }

                                desired = builder.ToImmutable();
                            }

                            using (await _mutex.WriterLockAsync())
                            {
                                if (modified)
                                {
                                    Assert(desired != null);

                                    if (deleted)
                                    {
                                        await InvalidateCoreAsync(cancellation: default);
                                    }
                                    else
                                    {
                                        // We have to temporarily invalidate the cache entry to ensure concistency.
                                        // Otherwise we have a valid cache entry that is not concistent with the global state, as we modify the global state now.
                                        // TODO: If _mutex is transformed in a reader-writer lock and all readers have to acquire the lock, we can skip this. Is that performant?

                                        // Invalidate cache entry without releasing the read-lock.
                                        _entry = null;
                                    }

                                    if (await _cacheManager._storage.UpdateEntryAsync(desired, entry, cancellation: default) != entry)
                                    {
                                        // The entry may not be changed concurrently when we own the write-lock, unless our session is terminated.
                                        // If in the future additional information like desired locking is stored, this may be changed.
                                        // This however changes the semantic of the lock. Currently the lock protects the complete entry from
                                        // beeing modified.
                                        throw new SessionTerminatedException();
                                    }

                                    entry = desired;
                                }

                                entry = await _cacheManager._lockManager.ReleaseWriteLockAsync(entry);

                                if (modified && !deleted)
                                {
                                    Assert(IsLockedEntry(entry));
                                    Assert(entry == null || _entry == null || entry.StorageVersion >= _entry.StorageVersion);
                                    _entry = entry;
                                    Assert(_entry == null || IsLockedEntry(_entry));
                                }
                            }

                            // Release _lockMutex
                            mutexUnlocker.Dispose();
                        }
                        catch (SessionTerminatedException) { throw; }
                        catch
                        {
                            _cacheManager._sessionOwner.Dispose();
                            throw;
                        }
                    }

                    // TODO: This is 90% a copy of UnlockAsync
                    async ValueTask FlushAsync(ReadOnlyMemory<byte> value, bool deleted)
                    {
                        try
                        {
                            var builder = entry.ToBuilder(_session);

                            if (deleted)
                            {
                                builder.MarkAsDeleted();
                            }
                            else if (entry.IsMarkedAsDeleted)
                            {
                                builder.Create(value);
                            }
                            else
                            {
                                builder.SetValue(value);
                            }

                            var desired = builder.ToImmutable();

                            using (await _mutex.WriterLockAsync())
                            {
                                if (deleted)
                                {
                                    await InvalidateCoreAsync(cancellation: default);
                                }
                                else
                                {
                                    // We have to temporarily invalidate the cache entry to ensure concistency.
                                    // Otherwise we have a valid cache entry that is not concistent with the global state, as we modify the global state now.
                                    // TODO: If _mutex is transformed in a reader-writer lock and all readers have to acquire the lock, we can skip this. Is that performant?

                                    // Invalidate cache entry without releasing the read-lock.
                                    _entry = null;
                                }

                                if (await _cacheManager._storage.UpdateEntryAsync(desired, entry, cancellation: default) != entry)
                                {
                                    // The entry may not be changed concurrently when we own the write-lock, unless our session is terminated.
                                    // If in the future additional information like desired locking is stored, this may be changed.
                                    // This however changed the semtaic of the lock. Currently the lock protects the complete entry from
                                    // beeing modified.
                                    throw new SessionTerminatedException();
                                }

                                entry = desired;

                                // TODO: Are we allow to update the cache while still owning the write-lock?
                                if (!deleted)
                                {
                                    Assert(IsLockedEntry(entry));
                                    Assert(entry == null || _entry == null || entry.StorageVersion >= _entry.StorageVersion);
                                    _entry = entry;
                                    Assert(_entry == null || IsLockedEntry(_entry));
                                }
                            }
                        }
                        catch (SessionTerminatedException) { throw; }
                        catch
                        {
                            _cacheManager._sessionOwner.Dispose();
                            throw;
                        }
                    }

                    var lockReleaserSource = LockedEntrySource.Allocate(entry.Value, !entry.IsMarkedAsDeleted, UnlockAsync, FlushAsync);
                    return new LockedEntry(Key, lockReleaserSource, LockType.Exclusive);
                }
                catch
                {
                    mutexUnlocker.Dispose();
                    throw;
                }
            }

            private async ValueTask<IStoredEntry> GetEntryAsync(CancellationToken cancellation)
            {
                // Volatile read op.
                var entry = _entry;

                if (!IsLockedEntry(entry))
                {
                    entry = await _cacheManager._storage.GetEntryAsync(Key, cancellation);

                    using (await _mutex.WriterLockAsync(cancellation))
                    {
                        if (IsLockedEntry(_entry))
                        {
                            return _entry;
                        }

                        if (entry != null && !entry.IsMarkedAsDeleted)
                        {
                            // We may already have a read-lock on the entry.
                            if (!IsLockedEntry(entry))
                            {
                                entry = await _cacheManager._lockManager.AcquireReadLockAsync(entry, cancellation);
                            }

                            Assert(entry == null || entry.IsMarkedAsDeleted || IsLockedEntry(entry));
                            _entry = entry;
                        }
                    }
                }

                return entry;
            }

            public async ValueTask InvalidateAsync(CancellationToken cancellation)
            {
                if (!IsLockedEntry(_entry))
                {
                    return;
                }

                using (await _mutex.WriterLockAsync(cancellation))
                {
                    await InvalidateCoreAsync(cancellation);
                }
            }

            private async ValueTask InvalidateCoreAsync(CancellationToken cancellation)
            {
                if (!IsLockedEntry(_entry))
                {
                    return;
                }

                _entry = await _cacheManager._lockManager.ReleaseReadLockAsync(_entry);

                Assert(!IsLockedEntry(_entry));
            }
        }

        private sealed class LockedEntrySource : ILockedEntrySource
        {
            private static readonly ObjectPool<LockedEntrySource> _pool = CreatePool();

            private readonly object _mutex = new object();

            private ValueTaskCompletionSource _unlockTaskSource;
            private ReadOnlyMemory<byte> _value;
            private bool _isExisting;
            private ValueTask _unlockTask;
            private bool _unlockStarted;
            private Func<bool, ReadOnlyMemory<byte>, bool, ValueTask> _unlockOperation;
            private bool _modified; // We could alternatively store the original value and compare it only when needed.
            private Func<ReadOnlyMemory<byte>, bool, ValueTask> _flushOperation;

            // After beeing created, this is incremented in Allocate to int.MinValue
            public int Token { get; private set; } = int.MaxValue;

            public void Unlock(int token)
            {
                if (token != Token)
                    throw new NotSupportedException();

                lock (_mutex)
                {
                    if (_unlockStarted)
                        return;

                    _unlockTask = UnlockInternalAsync();
                    _unlockStarted = true;
                }
            }

            public ValueTask GetUnlockTask(int token)
            {
                if (token != Token)
                    throw new NotSupportedException();

                return _unlockTaskSource.Task;
            }

            private async ValueTask UnlockInternalAsync()
            {
                try
                {
                    await _unlockOperation(_modified, _value, !_isExisting);
                }
                catch (OperationCanceledException exc)
                {
                    var cancellation = exc.CancellationToken;

                    if (cancellation.CanBeCanceled)
                    {
                        _unlockTaskSource.TrySetCanceled(cancellation);
                    }
                    else
                    {
                        _unlockTaskSource.TrySetCanceled();
                    }
                }
                catch (Exception exc)
                {
                    _unlockTaskSource.TrySetException(exc);
                }
                finally
                {
                    _unlockTaskSource.TrySetResult();
                    _pool.Return(this);
                }
            }

            public ReadOnlyMemory<byte> GetValue(int token)
            {
                if (token != Token)
                    throw new NotSupportedException();

                return _value;
            }

            public bool IsExisting(int token)
            {
                if (token != Token)
                    throw new NotSupportedException();

                return _isExisting;
            }

            public void CreateOrUpdate(int token, ReadOnlyMemory<byte> value)
            {
                if (token != Token)
                    throw new NotSupportedException();

                if (!_isExisting || !_value.Span.SequenceEqual(value.Span))
                {
                    _modified = true;
                }

                _isExisting = true;
                _value = value;
            }

            public void Delete(int token)
            {
                if (token != Token)
                    throw new NotSupportedException();

                if (_isExisting)
                {
                    _modified = true;
                }

                _isExisting = false;
                _value = ReadOnlyMemory<byte>.Empty;
            }

#if !SUPPORTS_TRANSACTIONS

            // This is a temporary addition that is needed for the coordination service to consistently create entries.
            // This will be removed when we have ACID support for multi-entry changes.
            public async ValueTask FlushAsync(int token, CancellationToken cancellation)
            {
                if (token != Token)
                    throw new NotSupportedException();

                if (!_modified)
                    return;

                if (_flushOperation == null)
                {
                    throw new NotSupportedException();
                }

                await _flushOperation(_value, !_isExisting);
                _modified = false;

            }

#endif

            private static ObjectPool<LockedEntrySource> CreatePool()
            {
                return new DefaultObjectPool<LockedEntrySource>(new LockedEntrySourcePoolPolicy());
            }

            private sealed class LockedEntrySourcePoolPolicy : IPooledObjectPolicy<LockedEntrySource>
            {
                public LockedEntrySource Create()
                {
                    return new LockedEntrySource();
                }

                public bool Return(LockedEntrySource obj)
                {
                    return obj.Token < int.MaxValue;
                }
            }

            internal static LockedEntrySource Allocate(
                ReadOnlyMemory<byte> value,
                bool isExisting,
                Func<bool, ReadOnlyMemory<byte>, bool, ValueTask> unlockOperation,
                Func<ReadOnlyMemory<byte>, bool, ValueTask> flushOperation)
            {
                var result = _pool.Get();
                unchecked { result.Token++; }
                result._value = value;
                result._isExisting = isExisting;
                result._unlockTask = default;
                result._unlockStarted = false;
                result._unlockOperation = unlockOperation;
                result._unlockTaskSource = ValueTaskCompletionSource.Create();
                result._modified = false;
                result._flushOperation = flushOperation;
                return result;
            }

            internal static LockedEntrySource Allocate(ReadOnlyMemory<byte> value, bool isExisting, Func<ValueTask> unlockOperation)
            {
                return Allocate(value, isExisting, (x, y, z) => unlockOperation(), flushOperation: null);
            }
        }
    }
}
