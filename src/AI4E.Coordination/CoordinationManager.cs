/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        CoordinationManager.cs 
 * Types:           (1) AI4E.Coordination.CoordinationManager'1
 *                  (2) AI4E.Coordination.CoordinationManager'1.Entry
 *                  (3) AI4E.Coordination.CoordinationManagerFactory'1
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   30.09.2018 
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
using AI4E.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

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

        private readonly IAsyncProcess _updateSessionProcess;
        private readonly IAsyncProcess _sessionCleanupProcess;

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
            _logger = logger;

            _options = optionsAccessor.Value ?? new CoordinationManagerOptions();
            _session = BuildSession();

            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess, start: true);
            _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess, start: true);
        }

        #endregion

        public IServiceProvider ServiceProvider => _serviceScope.ServiceProvider;

        #region Session management

        public ValueTask<Session> GetSessionAsync(CancellationToken cancellation)
        {
            return new ValueTask<Session>(_session.Task.WithCancellation(cancellation));
        }

        private DisposableAsyncLazy<Session> BuildSession()
        {
            return new DisposableAsyncLazy<Session>(
                factory: StartSessionAsync,
                disposal: TerminateSessionAsync,
                DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread);
        }

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

        private async Task SessionCleanupProcess(CancellationToken cancellation)
        {
            var session = await GetSessionAsync(cancellation);

            _logger?.LogTrace($"[{session}] Started session cleanup process.");

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var terminated = await _sessionManager.WaitForTerminationAsync(cancellation);

                    Assert(terminated != null);

                    // Our session is terminated or
                    // There are no session in the session manager. => Our session must be terminated.
                    if (terminated == session)
                    {
                        Dispose();
                    }
                    else
                    {
                        await CleanupSessionAsync(terminated, cancellation);
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { return; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{session}] Failure while cleaning up terminated sessions.");
                }
            }
        }

        private async Task UpdateSessionProcess(CancellationToken cancellation)
        {
            var session = await GetSessionAsync(cancellation);

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    await UpdateSessionAsync(session, cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{session}] Failure while updating session {session}.");
                }
            }
        }

        private async Task UpdateSessionAsync(Session session, CancellationToken cancellation)
        {
            var leaseLength = _options.LeaseLength;

            if (leaseLength <= TimeSpan.Zero)
            {
                leaseLength = CoordinationManagerOptions.LeaseLengthDefault;
                Assert(leaseLength > TimeSpan.Zero);
            }

            var leaseLengthHalf = new TimeSpan(leaseLength.Ticks / 2);

            if (leaseLengthHalf <= TimeSpan.Zero)
            {
                leaseLengthHalf = new TimeSpan(1);
            }

            Assert(session != null);

            var leaseEnd = _dateTimeProvider.GetCurrentTime() + leaseLength;

            try
            {
                await _sessionManager.UpdateSessionAsync(session, leaseEnd, cancellation);

                await Task.Delay(leaseLengthHalf);
            }
            catch (SessionTerminatedException)
            {
                Dispose();
            }
        }

        private async Task CleanupSessionAsync(Session session, CancellationToken cancellation)
        {
            _logger?.LogInformation($"[{await GetSessionAsync(cancellation)}] Cleaning up session '{session}'.");

            var entries = await _sessionManager.GetEntriesAsync(session, cancellation);

            await Task.WhenAll(entries.Select(async entry =>
            {
                await DeleteAsync(entry, version: default, recursive: false, cancellation);
                await _sessionManager.RemoveSessionEntryAsync(session, entry, cancellation);
            }));

            await _sessionManager.EndSessionAsync(session, cancellation);
        }


        #endregion

        public void Dispose()
        {
            _updateSessionProcess.Terminate();
            _sessionCleanupProcess.Terminate();

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

            await _lockManager.AcquireLocalReadLockAsync(path, cancellation);

            try
            {
                var entry = await _storage.GetEntryAsync(path, cancellation);

                if (entry == null)
                {
                    goto CleanupParentOfDeletedEntry;
                }

                entry = await _lockManager.AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    goto CleanupParentOfDeletedEntry;
                }

                try
                {
                    var updatedCacheEntry = _cache.UpdateEntry(cacheEntry, entry);

                    // If we cannot update the cache, f.e. due to an old cache entry version, the cache is invalidated, and we do not need the read-lock. => Free it.
                    // This must be synchronized with any concurrent read operations that 
                    // (1) Register a read-lock (No-op, as we currently own a read-lock)
                    // (2) Updates the cache entry successfully.
                    // (3) We are releasing the read-lock here
                    // In order to not leaves the coordination service in an inconsistent state, we have acquire a separate local read-lock on cache update.
                    if (!updatedCacheEntry.IsValid)
                    {
                        Assert(entry != null);
                        await _lockManager.ReleaseReadLockAsync(entry, cancellation);
                    }
                }
                catch
                {
                    Assert(entry != null);
                    await _lockManager.ReleaseReadLockAsync(entry, cancellation);
                    throw;
                }

                return entry;
            }
            finally
            {
                await _lockManager.ReleaseLocalReadLockAsync(path, cancellation);
            }

            // We do not want to do this under the local read-lock.
            CleanupParentOfDeletedEntry:

            // This operation is called for each child of an entry, when iterating the child collection.
            // We are in the case that the requested entry cannot be found. 
            // If this is part of the parents child collection, we clean this up now.
            await UpdateParentOfDeletedEntry(path, cancellation);
            return null;
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

            await _lockManager.AcquireLocalReadLockAsync(path, cancellation);

            try
            {
                entry = await _lockManager.AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    return null;
                }

                try
                {
                    _cache.AddEntry(entry);

                    return entry;
                }
                catch
                {
                    Assert(entry != null);

                    await _lockManager.ReleaseReadLockAsync(entry, cancellation);

                    throw;
                }
            }
            finally
            {
                await _lockManager.ReleaseLocalReadLockAsync(path, cancellation);
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

            // We have no parent entry.
            if (path.IsRoot)
            {
                (entry, created) = await TryCreateCoreAsync(path, value, modes, session, cancellation);
            }
            else
            {
                var parentPath = path.GetParentPath();
                var parent = await EnsureParentLock(parentPath, session, cancellation);
                try
                {
                    (entry, created) = await TryCreateChildAsync(path, parent, value, modes, session, cancellation);
                }
                finally
                {
                    Assert(parent != null);
                    await _lockManager.ReleaseWriteLockAsync(parent, cancellation);
                }
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

        private async Task<(IStoredEntry entry, bool created)> TryCreateChildAsync(CoordinationEntryPath path,
                                                                                   IStoredEntry parent,
                                                                                   ReadOnlyMemory<byte> value,
                                                                                   EntryCreationModes modes,
                                                                                   Session session,
                                                                                   CancellationToken cancellation)
        {
            Assert(parent != null);

            if (parent.EphemeralOwner != null)
            {
                throw new InvalidOperationException($"Unable to create the entry. The parent entry is an ephemeral entry and is not allowed to have child entries.");
            }

            var name = path.Segments.Last();
            await UpdateEntryAsync(_storedEntryManager.AddChild(parent, name, session), parent, cancellation);

            try
            {
                return await TryCreateCoreAsync(path, value, modes, session, cancellation);
            }
            catch (SessionTerminatedException) { throw; }
            catch
            {
                // This is not the root entry and the parent entry was found. 
                // We did not successfully create the entry.
                // Note that the operation MUST be performed under the lock COMPLETELY.
                try
                {
                    // If this operation gets canceled, the parent entry is not cleaned up. 
                    // This is not of a problem, because the parent entry is not guaranteed to be strongly consistent anyway.
                    // In case of cancellation, we want the original exception to be rethrown.
                    await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, name, session), parent, cancellation);
                }
                catch (OperationCanceledException) { }

                throw;
            }
        }

        private async Task<IStoredEntry> EnsureParentLock(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            IStoredEntry result;

            do
            {
                result = await LoadAndAcquireWriteLockAsync(path, cancellation);

                if (result != null)
                {
                    break;
                }

                var (e, c) = await TryCreateInternalAsync(path, ReadOnlyMemory<byte>.Empty, modes: default, session, cancellation);
            }
            while (true);


            Assert(result != null);
            Assert(result.WriteLock == session);
            Assert(_cache.GetEntry(path).LocalWriteLock.CurrentCount == 0);
            return result;
        }

        private async Task<(IStoredEntry entry, bool created)> TryCreateCoreAsync(CoordinationEntryPath path,
                                                                                  ReadOnlyMemory<byte> value,
                                                                                  EntryCreationModes modes,
                                                                                  Session session,
                                                                                  CancellationToken cancellation)
        {
            IStoredEntry comparand = null;

            // Lock the local-lock first.
            // Create will lock the write-lock for us.
            await _lockManager.AcquireLocalWriteLockAsync(path, cancellation);
            Assert(_cache.GetEntry(path).LocalWriteLock.CurrentCount == 0);

            try
            {
                try
                {
                    // Check if entry exists.
                    comparand = await _storage.GetEntryAsync(path, cancellation);
                }
                catch
                {
                    await _lockManager.ReleaseLocalWriteLockAsync(path, cancellation);
                    throw;
                }

                if (comparand != null)
                {
                    // There is already an entry present
                    return (comparand, false);
                }

                // In case of failure, we currently do not remove the session entry. 
                // We cannot just remove the session entry on failure, as we do not have a lock on the entry
                // and, hence, the entry may already exist or is created concurrently.
                // This is ok because the session entry is skipped if the respective entry is not found. 
                // This could lead to many dead entries, if we have lots of failures. But we assume that this case is rather rare.
                // If this is ever a big performance problem, we can use a form of "reference counting" of session entries.
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.AddSessionEntryAsync(session, path, cancellation);
                }

                var entry = _storedEntryManager.Create(path, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value.Span);
                Assert(entry != null);
                Assert(entry.WriteLock == session);

                comparand = await _storage.UpdateEntryAsync(entry, comparand: null, cancellation);

                if (comparand != null)
                {
                    // There is already an entry present
                    return (comparand, false);
                }

                // We created the entry successfully. => Release the write lock.
                entry = await _lockManager.ReleaseWriteLockAsync(entry, cancellation);

                Assert(entry.WriteLock == null);

                // We created the entry successfully.
                return (entry, true);
            }
            finally
            {
                if (comparand != null)
                {
                    Assert(comparand.WriteLock != session);

                    // There is already an entry present => Release the local lock only.
                    await _lockManager.ReleaseLocalWriteLockAsync(path, cancellation);
                }
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
                var entry = await LoadAndAcquireWriteLockAsync(path, cancellation);

                if (entry == null)
                {
                    return 0;
                }

                try
                {
                    return await DeleteInternalAsync(entry, session, version, recursive, cancellation);
                }
                finally
                {
                    // We must only release the write-lock if we could acquire it previously. 
                    // If the entry did not exist, this is not the case.
                    Assert(entry != null);
                    await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
                }
            }

            var parent = await LoadAndAcquireWriteLockAsync(path.GetParentPath(), cancellation);

            // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
            if (parent == null)
            {
                return 0;
            }

            try
            {
                var entry = await LoadAndAcquireWriteLockAsync(path, cancellation);

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
                }
                finally
                {
                    Assert(entry != null);
                    await _lockManager.ReleaseWriteLockAsync(entry, cancellation);
                }

                // The entry is deleted now, because WE deleted it.
                var name = path.Segments.Last();

                // This is not the root entry.

                // We can safely remove the reference from the parent entry, as the entry cannot be recreaed concurrently. 
                // (We own the write-lock for the parent entry, that must be allocated, before creating a child entry.)

                // If this operation gets canceled, the parent entry is not cleaned up. 
                // This is not of a problem, because the parent entry is not guaranteed to be strongly consistent anyway.
                await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, name, session), parent, cancellation);

                return version;
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
            // The entry is not existing.
            if (entry == null)
            {
                return default;
            }

            if (version != default && entry.Version != version)
            {
                return (entry, deleted: false);
            }

            var comparand = entry;

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

                var child = await LoadAndAcquireWriteLockAsync(childPath, cancellation);
                var originalChild = child;

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
                    Assert(originalChild != null);
                    await _lockManager.ReleaseWriteLockAsync(originalChild, cancellation);
                }

                // As we did not specify a version, the call must succeed.
                Assert(deleted);
                Assert(child == null);

                entry = _storedEntryManager.RemoveChild(entry, childName, session);
            }

            // Delete the entry
            await UpdateEntryAsync(_storedEntryManager.Remove(entry, session), comparand, cancellation);

            if (entry.EphemeralOwner != null)
            {
                await _sessionManager.RemoveSessionEntryAsync((Session)entry.EphemeralOwner, entry.Path, cancellation);
            }

            // We must remove the entry from the cache by ourselves here, 
            // as we do allow the session to hold a read-lock and a write-lock at the same time 
            // and hence do not wipe the entry from the cache on write lock acquirement.
            _cache.InvalidateEntry(entry.Path); // TODO: We actually want to remove the entry from the cache, but this also deletes the local write-lock, we own.
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

            var entry = await LoadAndAcquireWriteLockAsync(path, cancellation);

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
            if (path.IsRoot)
            {
                return;
            }

            var parentPath = path.GetParentPath();
            var child = path.Segments.Last();

            var parent = await _storage.GetEntryAsync(parentPath, cancellation);

            if (parent == null || !parent.Children.Contains(child))
            {
                return;
            }

            var session = await GetSessionAsync(cancellation);
            parent = await _lockManager.AcquireWriteLockAsync(parent, cancellation);

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

        private async Task<IStoredEntry> LoadAndAcquireWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            if (entry == null)
                return null;

            return await _lockManager.AcquireWriteLockAsync(entry, cancellation);
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
