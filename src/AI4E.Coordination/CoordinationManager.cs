/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        CoordinationManager.cs 
 * Types:           (1) AI4E.Coordination.CoordinationManager
 *                  (2) AI4E.Coordination.CoordinationManager.CacheEntry
 *                  (3) AI4E.Coordination.CoordinationManager.Entry
 *                  (4) AI4E.Coordination.CoordinationManager.Entry.ChildrenEnumerable
 *                  (5) AI4E.Coordination.CoordinationManager.Entry.ChildrenEnumerator
 *                  (6) AI4E.Coordination.CoordinationManager.MessageType
 *                  (7) AI4E.Coordination.CoordinationManager.Provider
 *                  (8) AI4E.Coordination.CoordinationManager.WaitDirectory'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   10.05.2018 
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
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

// TODO: Do we have to synchronize read access? See GetEntryAsync

namespace AI4E.Coordination
{
    public sealed partial class CoordinationManager<TAddress> : ICoordinationManager
    {
        #region Fields

        private readonly IServiceScope _serviceScope;
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly CoordinationEntryCache _cache;
        private readonly ICoordinationLockManager _lockManager;
        private readonly ILogger<CoordinationManager<TAddress>> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly DisposableAsyncLazy<Session> _session;
        private readonly CoordinationSessionManagement _sessionManagement;

        #endregion

        #region C'tor

        public CoordinationManager(IServiceScope serviceScope,
                                   ICoordinationStorage storage,
                                   IStoredEntryManager storedEntryManager,
                                   ISessionManager sessionManager,
                                   ISessionProvider sessionProvider,
                                   IDateTimeProvider dateTimeProvider,
                                   CoordinationEntryCache cache,
                                   ICoordinationLockManager lockManager,
                                   CoordinationSessionManagement sessionManagement,
                                   IOptions<CoordinationManagerOptions> optionsAccessor,
                                   ILogger<CoordinationManager<TAddress>> logger = null)
        {
            if (serviceScope == null)
                throw new ArgumentNullException(nameof(serviceScope));

            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (sessionProvider == null)
                throw new ArgumentNullException(nameof(sessionProvider));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (cache == null)
                throw new ArgumentNullException(nameof(cache));

            if (lockManager == null)
                throw new ArgumentNullException(nameof(lockManager));

            if (sessionManagement == null)
                throw new ArgumentNullException(nameof(sessionManagement));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _serviceScope = serviceScope;
            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _sessionManager = sessionManager;
            _sessionProvider = sessionProvider;
            _dateTimeProvider = dateTimeProvider;
            _cache = cache;
            _lockManager = lockManager;
            _sessionManagement = sessionManagement;
            _logger = logger;

            _options = optionsAccessor.Value ?? new CoordinationManagerOptions();
            _session = new DisposableAsyncLazy<Session>(
                factory: StartSessionAsync,
                disposal: TerminateSessionAsync,
                DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

        #endregion

        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        private async Task<Session> StartSessionAsync(CancellationToken cancellation)
        {
            var leaseLength = _options.LeaseLength;

            if (leaseLength <= TimeSpan.Zero)
            {
                leaseLength = CoordinationManagerOptions.LeaseLengthDefault;
                Assert(leaseLength > TimeSpan.Zero);
            }

            Session session;

            do
            {
                session = _sessionProvider.GetSession();

                Assert(session != null);
            }
            while (!await _sessionManager.TryBeginSessionAsync(session, leaseEnd: _dateTimeProvider.GetCurrentTime() + leaseLength, cancellation));

            _logger?.LogInformation($"[{session}] Started session.");

            return session;
        }

        private Task TerminateSessionAsync(Session session)
        {
            Assert(session != null);

            return _sessionManager.EndSessionAsync(session)
                                  .HandleExceptionsAsync(_logger);
        }

        public ValueTask<Session> GetSessionAsync(CancellationToken cancellation)
        {
            return new ValueTask<Session>(_session.Task.WithCancellation(cancellation));
        }

        public void Dispose()
        {
            _serviceScope.Dispose();
            _session.Dispose();
        }

        #region Get entry

        public async ValueTask<IEntry> GetAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            var entry = await GetEntryAsync(path, cancellation);

            if (entry == null)
            {
                return null;
            }

            return new Entry(this, entry);
        }

        private async ValueTask<IStoredEntry> GetEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            // First try to load the entry from the cache.
            var cacheEntry = _cache.GetEntry(path);

            // We have to check whether the cache entry is (still) valid.
            if (cacheEntry.IsValid)
            {
                return cacheEntry.Entry;
            }

            var result = await _lockManager.AcquireReadLockAsync(path, cancellation);

            if (result == null)
            {
                // TODO: Why do we perform this here?
                await UpdateParentOfDeletedEntry(path, cancellation);
                return null;
            }

            try
            {
                var updatedCacheEntry = _cache.UpdateEntry(cacheEntry, result);

                // TODO: If we cannot update the cache, f.e. due to an old cache entry version, the cache is invalidated, and we do not need the read-lock. => Free it.
                // This must be synchronized with any concurrent read operations that 
                // (1) Register a read-lock (No-op, as we currently own a read-lock)
                // (2) Updates the cache entry successfully.
                // (3) We are releasing the read-lock here
                // This leaves the coordination service in an inconsistent state, as we have cached an entry but do not have a read-lock for it.

                //if (!updatedCacheEntry.IsValid)
                //{
                //    await ReleaseReadLockAsync(result, cancellation);
                //}
            }
            catch
            {
                await _lockManager.ReleaseReadLockAsync(path, cancellation);
                throw;
            }

            return result;

            // TODO: Is it ok that we insert the data outside of the local lock?
            // TODO: Is it consistent this way? Is it possible that we concurrently allocate/deallocate read-locks? Do we have to synchronize this?
            // TODO: It is a bit ugly that the lock manager uses the cache entry to get the local-lock, and we are using it, to update the payload.

        }

        #endregion

        #region Create entry

        public async ValueTask<IEntry> CreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes, CancellationToken cancellation)
        {
            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var (entry, created) = await TryCreateInternalAsync(path,
                                                                value,
                                                                modes,
                                                                session,
                                                                cancellation);

            // There is already an entry present.
            if (!created)
            {
                throw new DuplicateEntryException(path);
            }

            Assert(entry != null);
            await AddToCacheAsync(entry, cancellation);

            return new Entry(this, entry);
        }

        public async ValueTask<IEntry> GetOrCreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes, CancellationToken cancellation)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var cachedEntry = await GetEntryAsync(path, cancellation);

            if (cachedEntry != null)
            {
                return new Entry(this, cachedEntry);
            }

            var (entry, _) = await TryCreateInternalAsync(path,
                                                          value,
                                                          modes,
                                                          session,
                                                          cancellation);

            Assert(entry != null);
            await AddToCacheAsync(entry, cancellation);

            return new Entry(this, entry);
        }

        private async Task<IStoredEntry> AddToCacheAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry == null)
                throw new ArgumentNullException(nameof(entry));

            var path = entry.Path;

            try
            {
                entry = await _lockManager.AcquireReadLockAsync(entry, cancellation);

                if (entry != null)
                {
                    _cache.AddEntry(entry);
                }

                return entry;
            }
            catch
            {
                await _lockManager.ReleaseReadLockAsync(path, cancellation);

                throw;
            }
        }

        private async Task<(IStoredEntry entry, bool created)> TryCreateInternalAsync(CoordinationEntryPath path,
                                                                                      ReadOnlyMemory<byte> value,
                                                                                      EntryCreationModes modes,
                                                                                      Session session,
                                                                                      CancellationToken cancellation)
        {
            Stopwatch watch = null;

            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            IStoredEntry entry;
            bool created;

            // We have a parent entry.
            if (!path.IsRoot)
            {
                var parentPath = path.GetParentPath();
                var parent = await EnsureParentLock(parentPath, session, cancellation);

                try
                {
                    Assert(parent != null);

                    if (parent.EphemeralOwner != null)
                    {
                        throw new InvalidOperationException($"Unable to create the entry. The parent entry is an ephemeral node and is not allowed to have child entries.");
                    }

                    var name = path.Segments.Last();
                    await UpdateEntryAsync(_storedEntryManager.AddChild(parent, name, session), parent, cancellation);

                    try
                    {
                        (entry, created) = await TryCreateCoreAsync(path, value, modes, session, cancellation);
                    }
                    catch (SessionTerminatedException) { throw; }
                    catch
                    {
                        // This is not the root node and the parent node was found. 
                        // We did not successfully create the entry.
                        // TODO: Can we do anything about the cancellation? Note that the operation MUST be performed under the lock COMPLETELY.
                        await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, name, session), parent, cancellation: default);
                        throw;
                    }
                }
                finally
                {
                    Assert(parent != null);
                    await _lockManager.ReleaseWriteLockAsync(parent, cancellation);
                }
            }
            else
            {
                (entry, created) = await TryCreateCoreAsync(path, value, modes, session, cancellation);
            }

            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                Assert(watch != null);
                watch.Stop();

                if (created)
                {
                    _logger?.LogTrace($"Creating the entry '{path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
                }
                else
                {
                    _logger?.LogTrace($"Creating the entry '{path.EscapedPath}' failed. The path was already present. Operation took {watch.ElapsedMilliseconds}ms.");
                }
            }

            return (entry, created);
        }

        private async Task<IStoredEntry> EnsureParentLock(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            IStoredEntry result;

            while ((result = await _lockManager.AcquireWriteLockAsync(path, cancellation)) == null)
            {
                await TryCreateInternalAsync(path, ReadOnlyMemory<byte>.Empty, modes: default, session, cancellation);
            }

            Assert(result != null);
            Assert(result.WriteLock == session);
            return result;
        }

        private async Task<(IStoredEntry entry, bool created)> TryCreateCoreAsync(CoordinationEntryPath path,
                                                                                  ReadOnlyMemory<byte> value,
                                                                                  EntryCreationModes modes,
                                                                                  Session session,
                                                                                  CancellationToken cancellation)
        {
            // Lock the local-lock first.
            // Create will lock the write-lock for us.
            await _lockManager.AcquireLocalLockAsync(path, cancellation);

            var entry = _storedEntryManager.Create(path, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value.Span);
            var comparand = await _storage.UpdateEntryAsync(entry, comparand: null, cancellation);

            try
            {
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.AddSessionEntryAsync(session, path, cancellation);
                }

                // In case of failure, we currently do not remove the session entry. 
                // We cannot just remove the session entry on failure, as we do not have a lock on the entry
                // and, hence, the entry may already exist or is created concurrently.
                // This is ok because the session entry is skipped if the respective entry is not found. 
                // This could lead to many dead entries, if we have lots of failures. But we assume that this case is rather rare.
                // If this is ever a big performance problem, we can use a form a "reference counting" of session entries.
            }
            finally
            {
                // There is already an entry present => Release the local lock only.
                if (comparand != null)
                {
                    await _lockManager.ReleaseLocalLockAsync(path, cancellation);
                }
                else
                {
                    await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
                }
            }

            // There is already an entry present
            if (comparand != null)
            {
                return (comparand, false);
            }
            else
            {
                return (entry, true);
            }
        }

        #endregion

        #region Delete entry

        public async ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version, bool recursive, CancellationToken cancellation)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Deleting entry '{path.EscapedPath.ConvertToString()}'.");

            // There is no parent entry.
            if (path.IsRoot)
            {
                var entry = await _lockManager.AcquireWriteLockAsync(path, cancellation);
                try
                {
                    return await DeleteInternalAsync(entry, session, version, recursive, cancellation);
                }
                finally
                {
                    Assert(entry != null);
                    await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
                }
            }

            var parent = await _lockManager.AcquireWriteLockAsync(path.GetParentPath(), cancellation);

            try
            {
                // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                if (parent == null)
                {
                    return 0;
                }

                var entry = await _lockManager.AcquireWriteLockAsync(path, cancellation);

                if (entry == null)
                {
                    return 0;
                }

                try
                {
                    var result = await DeleteInternalAsync(entry, session, version, recursive, cancellation);

                    // The entry was already deleted.
                    if (result == 0)
                    {
                        return 0;
                    }

                    // Version conflict.
                    if (result != version)
                    {
                        return result;
                    }

                    // The entry is deleted now, because WE deleted it.
                    var name = path.Segments.Last();
                    await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, name, session), parent, cancellation);

                    return version;
                }
                finally
                {
                    Assert(entry != null);
                    await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
                }
            }
            finally
            {
                Assert(parent != null);
                await _lockManager.ReleaseWriteLockAsync(parent, cancellation);
            }
        }

        private async ValueTask<int> DeleteInternalAsync(IStoredEntry entry, Session session, int version, bool recursive, CancellationToken cancellation)
        {
            bool deleted;

            (entry, deleted) = await DeleteCoreAsync(entry, session, version, recursive, cancellation);

            // If we did not specify a version, there are two possible cases:
            // * The call must have succeeded
            // * The entry was not present (already deleted)
            Assert(version != 0 || deleted || entry == null);

            // The entry is not existing.
            if (entry == null)
            {
                return 0;
            }

            if (!deleted)
            {
                return entry.Version;
            }

            return version;

        }

        // Deleted an entry without checking the input params and without locking the dispose lock.
        // The operation performs a recursive operation if the recursive parameter is true, throws an exception otherwise if there are child entries present.
        // The operation ensured consistency by locking the entry.
        // Return values:
        // entry: null if the delete operation succeeded or if the entry is not present
        // deleted: true, if the operation succeeded, false otherwise. Check the entry result in this case.
        private async Task<(IStoredEntry entry, bool deleted)> DeleteCoreAsync(IStoredEntry entry, Session session, int version, bool recursive, CancellationToken cancellation)
        {
            //entry = await AcquireWriteLockAsync(entry, cancellation);

            // The entry is not existing.
            if (entry == null)
            {
                return default;
            }

            if (version != default && entry.Version != version)
            {
                return (entry, deleted: false);
            }

            // Check whether there are child entries
            // It is important that all coordination manager instances handle the recursive operation in the same oder
            // (they must process all children in the exact same order) to prevent dead-lock situations.
            foreach (var childName in entry.Children)
            {
                bool deleted;
                // Recursively delete all child entries. 
                // The delete operation is not required to remove the child name entry in the parent entry, as the parent entry is  to be deleted anyway.
                // In the case that we cannot proceed (our session terminates f.e.), we do not guarantee that the child names collection is strongly consistent anyway.

                // First load the child entry.
                var childPath = entry.Path.GetChildPath(childName);
                var child = await _lockManager.AcquireWriteLockAsync(childPath, cancellation);

                // The child-names collection is not guaranteed to be strongly consistent.
                if (child == null)
                {
                    continue;
                }

                try
                {
                    // Check whether we allow recursive delete operation.
                    // This cannot be done upfront, 
                    // as the child-names collection is not guaranteed to be strongly consistent.
                    // The child names collection may contain child names but the childs are not present actually.
                    // => We check for the recursive option if we find any child that is present actually.
                    if (!recursive)
                    {
                        throw new InvalidOperationException("An entry that contains child entries cannot be deleted.");
                    }

                    (child, deleted) = await DeleteCoreAsync(child, session, version: default, recursive: true, cancellation);
                }
                finally
                {
                    Assert(child != null);
                    await _lockManager.ReleaseWriteLockAsync(child, cancellation);
                }

                // As we did not specify a version, the call must succeed.
                Assert(deleted);
                Assert(child == null);
            }

            // Delete the entry
            await UpdateEntryAsync(_storedEntryManager.Remove(entry, session), entry, cancellation);

            if (entry.EphemeralOwner != null)
            {
                await _sessionManager.RemoveSessionEntryAsync((Session)entry.EphemeralOwner, entry.Path, cancellation);
            }

            // We must remove the entry from the cache by ourselves here, 
            // as we do allow the session to hold a read-lock and a write-lock at the same time 
            // and hence do not wipe the entry from the cache on write lock acquirement.
            _cache.RemoveEntry(entry.Path);
            return (entry: null, deleted: true);
        }

        #endregion

        #region Set entry value

        public async ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version, CancellationToken cancellation)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var entry = await _lockManager.AcquireWriteLockAsync(path, cancellation);

            if (entry == null)
            {
                throw new EntryNotFoundException(path);
            }

            try
            {
                if (version != default && entry.Version != version)
                {
                    return entry.Version;
                }

                await UpdateEntryAsync(_storedEntryManager.SetValue(entry, value.Span, session), entry, cancellation);
                return version;
            }
            finally
            {
                Assert(entry != null);
                await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
            }
        }

        #endregion

        private async Task UpdateParentOfDeletedEntry(CoordinationEntryPath path, CancellationToken cancellation)
        {
            if (!path.IsRoot)
            {
                var parentPath = path.GetParentPath();
                var child = path.Segments.Last();

                var parent = await _storage.GetEntryAsync(parentPath, cancellation);

                if (parent != null && parent.Children.Contains(child))
                {
                    var session = await GetSessionAsync(cancellation);

                    // TODO: Can this be further optimized? We already loaded parent.
                    parent = await _lockManager.AcquireWriteLockAsync(path, cancellation);

                    if (parent == null)
                    {
                        return;
                    }

                    try
                    {
                        var childPath = path.GetChildPath(child);
                        var childEntry = await _storage.GetEntryAsync(childPath, cancellation);

                        if (childEntry == null)
                        {
                            await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, child, session), parent, cancellation);
                        }
                    }
                    finally
                    {
                        Assert(parent != null);
                        await _lockManager.ReleaseWriteLockAsync(parent, cancellation);
                    }
                }
            }
        }

        private async Task UpdateEntryAsync(IStoredEntry value, IStoredEntry comparand, CancellationToken cancellation)
        {
            var result = await _storage.UpdateEntryAsync(value, comparand, cancellation);

            // We are holding the exclusive lock => No one else can alter the entry.
            // The only exception is that out session terminates.
            if (!StoredEntryUtil.AreVersionEqual(result, comparand))
            {
                throw new SessionTerminatedException();
            }
        }

        private sealed class Entry : IEntry
        {
            public Entry(ICoordinationManager coordinationManager, IStoredEntry entry)
            {
                Assert(coordinationManager != null);
                Assert(entry != null);

                CoordinationManager = coordinationManager;

                Path = entry.Path;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Value = entry.Value;
                Children = entry.Children;
            }

            public CoordinationEntryPathSegment Name => Path.Segments.LastOrDefault();

            public CoordinationEntryPath Path { get; }

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public ReadOnlyMemory<byte> Value { get; }

            public IReadOnlyList<CoordinationEntryPathSegment> Children { get; }

            public CoordinationEntryPath ParentPath => Path.GetParentPath();

            public ICoordinationManager CoordinationManager { get; }
        }
    }

    public sealed class CoordinationManagerFactory<TAddress> : ICoordinationManagerFactory
    {
        private readonly IServiceProvider _serviceProvider;

        public CoordinationManagerFactory(IServiceProvider serviceProvider)
        {
            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _serviceProvider = serviceProvider;
        }

        public ICoordinationManager CreateCoordinationManager()
        {
            var scope = _serviceProvider.CreateScope();
            return ActivatorUtilities.CreateInstance<CoordinationManager<TAddress>>(scope.ServiceProvider, scope);
        }
    }
}
