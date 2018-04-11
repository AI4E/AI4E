/* Summary
 * --------------------------------------------------------------------------------------------------------------------
 * Filename:        CoordinationManager.cs 
 * Types:           (1) AI4E.Coordination.CoordinationManager
 *                  (2) AI4E.Coordination.CoordinationManager.Entry
 * Version:         1.0
 * Author:          Andreas Trütschel
 * Last modified:   11.04.2018 
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    // TODO: we need some kind of garbage collector that looks for dead session and clean them up.
    // TODO: Ephemeral nodes are not deleted on session termination currently.
    // TODO: Race condition: 
    // A concurrent read and write request. 
    // 1. The read request reads the entry.
    // 2. The write request locks the entry and sends an unlock message.
    // 3. The unlock message is received and the cache entry is cleared.
    // 4. The read request succeeds and places the entry into the cache.
    public sealed class CoordinationManager : ICoordinationManager, IAsyncDisposable
    {
        private const char _seperatorChar = '/';
        private const string _seperatorString = "/";
        private static readonly char[] _pathSeperators = { _seperatorChar, '\\' };
        private static readonly TimeSpan _leaseLength = TimeSpan.FromSeconds(30);
        private static readonly TimeSpan _leaseLengthHalf = new TimeSpan(_leaseLength.Ticks / 2);

        private readonly ICoordinationStorage _storage;
        private readonly ISessionManager _sessionManager;
        private readonly ICoordinationServiceCallback _callback;
        private readonly ISessionProvider _sessionProvider;
        private readonly ILogger<CoordinationManager> _logger;
        private readonly ConcurrentDictionary<string, ICoordinationEntry> _entries;
        private readonly AsyncProcess _updateSessionProcess;
        private readonly AsyncInitializationHelper<string> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public CoordinationManager(ICoordinationStorage storage,
                                   ISessionManager sessionManager,
                                   ICoordinationServiceCallback callback,
                                   ISessionProvider sessionProvider,
                                   ILogger<CoordinationManager> logger)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (callback == null)
                throw new ArgumentNullException(nameof(callback));

            if (sessionProvider == null)
                throw new ArgumentNullException(nameof(sessionProvider));

            _storage = storage;
            _sessionManager = sessionManager;
            _callback = callback;
            _sessionProvider = sessionProvider;
            _logger = logger;
            _entries = new ConcurrentDictionary<string, ICoordinationEntry>();

            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess);
            _initializationHelper = new AsyncInitializationHelper<string>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public async Task<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            return await _initializationHelper.Initialization.WithCancellation(cancellation);
        }

        #region SessionManagement

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
                    _logger.LogWarning(exc, $"Failure while updating session {session}.");
                }
            }
        }

        private async Task UpdateSessionAsync(string session, CancellationToken cancellation)
        {
            Assert(session != null);

            var leaseEnd = DateTime.Now + _leaseLength;

            try
            {
                await _sessionManager.UpdateSessionAsync(session, leaseEnd, cancellation);

                await Task.Delay(_leaseLengthHalf);
            }
            catch (SessionTerminatedException)
            {
                Dispose();
            }
        }

        #endregion

        #region Initialization

        private async Task<string> InitializeInternalAsync(CancellationToken cancellation)
        {
            string session;

            do
            {
                session = _sessionProvider.GetSession();

                Assert(session != null);
            }
            while (!await _sessionManager.TryBeginSessionAsync(session, cancellation));

            await _updateSessionProcess.StartAsync(cancellation);

            return session;
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposeHelper.Disposal;

        public void Dispose()
        {
            _disposeHelper.Dispose();
        }

        public Task DisposeAsync()
        {
            return _disposeHelper.DisposeAsync();
        }

        private async Task DisposeInternalAsync()
        {
            var success = false;
            var session = string.Empty;

            try
            {
                try
                {
                    (success, session) = await _initializationHelper.CancelAsync();
                }
                finally
                {
                    await _updateSessionProcess.TerminateAsync();
                }
            }
            finally
            {
                if (success)
                {
                    await _sessionManager.EndSessionAsync(session);
                }
            }
        }

        #endregion

        #region Read entry

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var normalizedPath = NormalizePath(path);
            var entry = await GetEntryAsync(normalizedPath, cancellation);

            if (entry == null)
            {
                return null;
            }

            return new Entry(entry);
        }

        private async Task<ICoordinationEntry> GetEntryAsync(string path, CancellationToken cancellation = default)
        {
            Assert(path != null);

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                if (_entries.TryGetValue(path, out var value))
                {
                    return value;
                }

                value = await LoadEntryAsync(path, combinedCancellationSource.Token);

                if (value != null)
                {
                    _entries.TryUpdate(path, value, null);
                }

                return value;
            }
        }

        private async Task<ICoordinationEntry> LoadEntryAsync(string path, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            try
            {
                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    var parentPath = GetParentPath(path, out var name, normalize: false);

                    var parent = await _storage.GetEntryAsync(parentPath, cancellation);

                    if (parent != null && parent.Childs.Contains(name))
                    {
                        await RemoveChildEntryAsync(parent, name, cancellation);
                    }
                }

                return entry;
            }
            catch
            {
                await ReleaseReadLockAsync(entry);

                throw;
            }
        }

        #endregion

        #region Create entry

        public async Task CreateAsync(string path, byte[] value, EntryCreationModes modes = default, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var normalizedPath = NormalizePath(path);
            var parentPath = GetParentPath(normalizedPath, out var name, normalize: false);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = _storage.CreateEntry(normalizedPath, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value);
                var parent = (parentPath != null) ? await _storage.GetEntryAsync(parentPath, combinedCancellationSource.Token) : null;

                Assert(parentPath == null || parent != null);
                Assert(entry != null);

                try
                {
                    if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                    {
                        await _sessionManager.AddSessionEntryAsync(session, normalizedPath, combinedCancellationSource.Token);
                    }

                    try
                    {
                        // This is not the root node.
                        if (parent != null)
                        {
                            parent = await AcquireWriteLockAsync(parent, cancellation);

                            if (parent == null)
                            {
                                throw new InvalidOperationException($"Unable to create the entry. The parent entry with the path '{parentPath}' cannot be found.");
                            }

                            if (parent.EphemeralOwner != null)
                            {
                                throw new InvalidOperationException($"Unable to create the entry. The parent entry is an ephemeral node and is not allowed to have child entries.");
                            }

                            var result = _storage.UpdateEntryAsync(parent, parent.AddChild(name), cancellation);

                            // We are holding the exclusive lock => No one else can alter the entry.
                            // The only exception is that out session terminates.
                            if (result != entry)
                            {
                                throw new SessionTerminatedException();
                            }

                            Assert(result == entry);
                        }

                        entry = await CreateCoreAsync(entry, combinedCancellationSource);

                        // There is already an entry present.
                        if (entry == null)
                        {
                            throw new DuplicateEntryException(normalizedPath);
                        }

                    }
                    catch (SessionTerminatedException) { throw; }
                    catch (DuplicateEntryException) { throw; }
                    catch
                    {
                        // This is not the root node and the parent node was found. 
                        // We did not successfully create the entry.
                        if (parent != null && entry != null)
                        {
                            await RemoveChildEntryAsync(parent, name, cancellation: default);
                        }

                        throw;
                    }
                    finally
                    {
                        // Releasing a write lock we do not own is a no op.
                        await ReleaseWriteLockAsync(parent);
                    }
                }
                catch (SessionTerminatedException) { throw; }
                catch (DuplicateEntryException) { throw; }
                catch
                {
                    if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                    {
                        await _sessionManager.RemoveSessionEntryAsync(session, normalizedPath, combinedCancellationSource.Token);
                    }

                    throw;
                }
            }
        }

        private async Task<ICoordinationEntry> CreateCoreAsync(ICoordinationEntry entry, CancellationTokenSource cancellation)
        {
            Assert(entry != null);

            try
            {
                var result = await _storage.UpdateEntryAsync(comparand: null, entry, cancellation.Token);

                // There is already an entry present
                if (result != null)
                {
                    entry = null;
                }

                return entry;
            }
            finally
            {
                // If we created the entry successfully, we own the write lock and must unlock now.
                // If an entry was already present, we must not release the write lock (we do not own it).
                // entry is null in this case and ReleaseWriteLockAsync is a no-op.
                await ReleaseWriteLockAsync(entry);
            }
        }

        private async Task<ICoordinationEntry> RemoveChildEntryAsync(ICoordinationEntry entry, string child, CancellationToken cancellation)
        {
            Assert(child != null);

            try
            {
                entry = await AcquireWriteLockAsync(entry, cancellation);

                if (entry == null)
                {
                    return null;
                }

                var childEntry = await _storage.GetEntryAsync(GetChildPath(entry.Path, child, normalize: false), cancellation);

                if (childEntry == null)
                {
                    var result = _storage.UpdateEntryAsync(entry, entry.RemoveChild(child), cancellation);

                    // We are holding the exclusive lock => No one else can alter the entry.
                    // The only exception is that out session terminates.
                    if (result != entry)
                    {
                        throw new SessionTerminatedException();
                    }
                }

                return entry;
            }
            finally
            {
                // Releasing a write lock we do not own is a no op.
                await ReleaseWriteLockAsync(entry);
            }
        }

        #endregion

        #region Delete entry

        public async Task<int> DeleteAsync(string path, int version, bool recursive, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var normalizedPath = NormalizePath(path);
            var parentPath = GetParentPath(normalizedPath, out var name, normalize: false);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(normalizedPath, combinedCancellationSource.Token);
                var parent = (parentPath != null) ? await _storage.GetEntryAsync(parentPath, combinedCancellationSource.Token) : null;
                var isEphemeral = entry.EphemeralOwner != null;

                Assert(parentPath == null || parent != null);

                try
                {
                    if (parent != null)
                    {
                        parent = await AcquireWriteLockAsync(parent, combinedCancellationSource.Token);

                        // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                        if (parent == null)
                        {
                            return 0;
                        }
                    }

                    try
                    {
                        entry = await AcquireWriteLockAsync(entry, combinedCancellationSource.Token);

                        // The entry is not existing.
                        if (entry == null)
                        {
                            return 0;
                        }

                        if (version != default && entry.Version != version)
                        {
                            return entry.Version;
                        }

                        // Delete the entry
                        {
                            var result = _storage.UpdateEntryAsync(entry, entry.Remove(), combinedCancellationSource.Token);

                            // We are holding the exclusive lock => No one else can alter the entry.
                            // The only exception is that out session terminates.
                            if (result != entry)
                            {
                                throw new SessionTerminatedException();
                            }

                            entry = null;
                        }

                        try
                        {
                            // Remove the entry from its parent
                            if (parent != null)
                            {
                                var result = _storage.UpdateEntryAsync(parent, parent.RemoveChild(name), combinedCancellationSource.Token);

                                // We are holding the exclusive lock => No one else can alter the parent.
                                // The only exception is that out session terminates.
                                if (result != parent)
                                {
                                    throw new SessionTerminatedException();
                                }
                            }
                        }
                        finally
                        {
                            if (isEphemeral)
                            {
                                await _sessionManager.RemoveSessionEntryAsync(session, normalizedPath, combinedCancellationSource.Token);
                            }
                        }

                        return 0;
                    }
                    finally
                    {
                        // Releasing a write lock we do not own is a no op.
                        await ReleaseWriteLockAsync(entry);
                    }
                }
                finally
                {
                    await ReleaseWriteLockAsync(parent);
                }
            }
        }

        #endregion

        #region Set entry value

        public async Task<int> SetValueAsync(string path, byte[] value, int version, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (value == null)
                throw new ArgumentNullException(nameof(value));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var normalizedPath = NormalizePath(path);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(normalizedPath, combinedCancellationSource.Token);

                try
                {
                    entry = await AcquireWriteLockAsync(entry, cancellation);

                    if (entry == null)
                    {
                        throw new EntryNotFoundException(normalizedPath);
                    }

                    if (version != default && entry.Version != version)
                    {
                        return entry.Version;
                    }

                    var result = _storage.UpdateEntryAsync(entry, entry.SetValue(value.ToImmutableArray()), combinedCancellationSource.Token);

                    // We are holding the exclusive lock => No one else can alter the entry.
                    // The only exception is that out session terminates.
                    if (result != entry)
                    {
                        throw new SessionTerminatedException();
                    }
                }
                finally
                {
                    // Releasing a write lock we do not own is a no op.
                    await ReleaseWriteLockAsync(entry);
                }
            }

            return version;
        }

        #endregion

        #region Locking

        private async Task<ICoordinationEntry> AcquireReadLockAsync(ICoordinationEntry entry, CancellationToken cancellation)
        {
            ICoordinationEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            do
            {
                start = await WaitForWriteLockReleaseAsync(entry, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Assert(start.WriteLock == null);

                desired = start.AcquireReadLock(session);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (start != entry);

            return desired;
        }

        private async Task<ICoordinationEntry> ReleaseReadLockAsync(ICoordinationEntry entry)
        {
            ICoordinationEntry start, desired;

            var session = await GetSessionAsync();

            do
            {
                start = entry;

                // The entry was deleted (concurrently).
                if (start == null || !start.ReadLocks.Contains(session))
                {
                    return null;
                }

                Assert(start.WriteLock == null);

                desired = start.ReleaseReadLock(session);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation: _disposeHelper.DisposalRequested);
            }
            while (start != entry);

            return desired;
        }

        private async Task<ICoordinationEntry> AcquireWriteLockAsync(ICoordinationEntry entry, CancellationToken cancellation)
        {
            ICoordinationEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            do
            {
                // WaitForLockableAsync updates the session in order that there is enough time to complete the write operation, without the session to terminate.
                start = await WaitForWriteLockReleaseAsync(entry, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Assert(start.WriteLock == null);

                desired = start.AcquireWriteLock(session);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (entry != start);

            try
            {
                entry = await WaitForReadLocksReleaseAsync(desired, cancellation);
                return entry;
            }
            finally
            {
                await ReleaseReadLockAsync(entry);
            }

        }

        private async Task<ICoordinationEntry> ReleaseWriteLockAsync(ICoordinationEntry entry)
        {
            ICoordinationEntry start, desired;

            var session = await GetSessionAsync();

            do
            {
                start = entry;

                // The entry was deleted (concurrently) or the session does not own the write lock.
                if (start == null || start.WriteLock != session)
                {
                    return start;
                }

                desired = start.ReleaseWriteLock();

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation: _disposeHelper.DisposalRequested);
            }
            while (entry != start);

            return desired;
        }

        private async Task<ICoordinationEntry> WaitForWriteLockReleaseAsync(ICoordinationEntry entry, CancellationToken cancellation)
        {
            // The entry was deleted (concurrently).
            while (entry != null)
            {
                var writeLock = entry.WriteLock;

                // If (entry.WriteLock == session) we MUST wait till the lock is released
                // and acquired again in order that no concurrency conflicts may occur.
                if (writeLock == null)
                {
                    return entry;
                }


                var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

                try // Await the end of the session that holds 'writeLock' or the lock release, whichever occurs first.
                {
                    var lockRelease = SpinWaitAsync(async () =>
                    {
                        entry = await _storage.GetEntryAsync(entry.Path, cancellation);
                        return entry == null || entry.WriteLock == null;
                    }, combinedCancellation.Token);

                    var sessionTermination = WaitForSessionTermination(entry.Path, writeLock, combinedCancellation.Token);

                    var completed = await Task.WhenAny(sessionTermination, lockRelease);

                    if (completed == sessionTermination)
                    {
                        entry = await _storage.GetEntryAsync(entry.Path, cancellation);
                    }
                }
                finally
                {
                    combinedCancellation.Cancel();
                }
            }

            return null;
        }

        private async Task<ICoordinationEntry> WaitForReadLocksReleaseAsync(ICoordinationEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            var readLocks = entry.ReadLocks;

            // Send a message to each of 'readLocks' to ask for lock release and await the end of the session or the lock release, whichever occurs first.
            await Task.WhenAll(readLocks.Select(session => WaitForReadLockRelease(entry.Path, session, cancellation)));

            // The unlock of the 'readLocks' will alter the db, so we have to read the entry again.
            entry = await _storage.GetEntryAsync(entry.Path, cancellation);

            // We are holding the write-lock => The entry must not be deleted concurrently.
            if (entry == null || entry.ReadLocks.Length != 0)
            {
                throw new SessionTerminatedException();
            }

            return entry;
        }

        /// <summary>
        /// Asynchronously waits for the specified session to terminate and releases all locks held by the session in regards to the entry with the specified path.
        /// </summary>
        /// <param name="key">The path to the entry the session holds locks of.</param>
        /// <param name="session">The session thats termination shall be awaited.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        /// <exception cref="SessionTerminatedException">Thrown if <paramref name="session"/> is the local session and the session terminated before the operation is cancelled.</exception>
        private async Task WaitForSessionTermination(string key, string session, CancellationToken cancellation)
        {
            var localSession = await GetSessionAsync(cancellation);

            await _sessionManager.WaitForTerminationAsync(session, cancellation);

            // We waited for ourself to terminate => We are terminated now.
            if (session == localSession)
            {
                throw new SessionTerminatedException();
            }

            await CleanupSessionAsync(session, cancellation);

            ICoordinationEntry entry = await _storage.GetEntryAsync(key, cancellation),
                               start,
                               desired;

            do
            {
                cancellation.ThrowIfCancellationRequested();

                start = entry;

                if (start == null)
                {
                    return;
                }

                if (entry.WriteLock == session)
                {
                    desired = start.ReleaseWriteLock();
                }
                else if (entry.ReadLocks.Contains(session))
                {
                    desired = start.ReleaseReadLock(session);
                }
                else
                {
                    return;
                }

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (start != entry);
        }

        private async Task CleanupSessionAsync(string session, CancellationToken cancellation)
        {
            var entries = await _sessionManager.GetEntriesAsync(session, cancellation);

            await Task.WhenAll(entries.Select(async entry =>
            {
                await DeleteAsync(entry, version: default, recursive: false, cancellation);
                await _sessionManager.RemoveSessionEntryAsync(session, entry, cancellation);
            }));
        }

        /// <summary>
        /// Asynchronously waits for a single read lock to be released.
        /// </summary>
        /// <param name="path">The path to the entry that the specified session holds a read lock of.</param>
        /// <param name="session">The session that holds the read lock.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was cancelled.</exception>
        private async Task WaitForReadLockRelease(string path, string session, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            if (entry == null || !entry.ReadLocks.Contains(session))
            {
                return;
            }

            await _callback.ReleaseReadLockAsync(path, session, cancellation);

            var combinedCancellation = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            try
            {
                var sessionTermination = WaitForSessionTermination(path, session, combinedCancellation.Token);

                var lockRelease = SpinWaitAsync(async () =>
                {
                    entry = await _storage.GetEntryAsync(path, cancellation);
                    return entry == null || !entry.ReadLocks.Contains(session);
                }, cancellation);

                var completed = await Task.WhenAny(sessionTermination, lockRelease);

                if (completed == sessionTermination)
                {
                    entry = await _storage.GetEntryAsync(entry.Path, cancellation);
                }
            }
            finally
            {
                combinedCancellation.Cancel();
            }
        }

        private async Task SpinWaitAsync(Func<Task<bool>> predicate, CancellationToken cancellation)
        {
            var timeToWait = new TimeSpan(100 * TimeSpan.TicksPerMillisecond);
            var timeToWaitMax = new TimeSpan(12800 * TimeSpan.TicksPerMillisecond);

            while (!await predicate())
            {
                cancellation.ThrowIfCancellationRequested();

                await Task.Delay(timeToWait, cancellation);

                if (timeToWait < timeToWaitMax)
                    timeToWait = new TimeSpan(timeToWait.Ticks * 2);
            }
        }

        #endregion

        private string NormalizePath(string path)
        {
            Assert(path != null);

            var segments = path.Split(_pathSeperators, StringSplitOptions.None);

            var normalizedPathBuilder = new StringBuilder();

            foreach (var segment in segments)
            {
                int segmentStart, segmentEnd;

                for (segmentStart = 0; segmentStart < segment.Length && char.IsWhiteSpace(segment[segmentStart]); segmentStart++) ;

                if (segmentStart == segment.Length - 1)
                    continue;

                for (segmentEnd = segment.Length - 1; segmentEnd > segmentStart && char.IsWhiteSpace(segment[segmentEnd]); segmentEnd--) ;

                var length = segmentEnd - segmentStart + 1;

                normalizedPathBuilder.Append(_seperatorChar);
                normalizedPathBuilder.Append(segment);
            }

            if (normalizedPathBuilder.Length == 0)
                return null;

            return normalizedPathBuilder.ToString();
        }

        private string GetParentPath(string path, out string name, bool normalize = true)
        {
            Assert(path != null);

            var normalizedPath = path;

            if (normalize)
            {
                normalizedPath = NormalizePath(path);
            }

            var lastIndexOfSeparator = normalizedPath.LastIndexOf(_seperatorChar);

            // This is the root node.
            if (lastIndexOfSeparator == 0)
            {
                name = string.Empty;
                return normalizedPath;
            }

            // Separator is not last char
            Assert(lastIndexOfSeparator < normalizedPath.Length - 1);

            name = normalizedPath.Substring(lastIndexOfSeparator + 1);
            return normalizedPath.Substring(0, lastIndexOfSeparator);
        }

        private string GetChildPath(string path, string childName, bool normalize = true)
        {
            var normalizedPath = path;
            if (normalize)
            {
                normalizedPath = NormalizePath(path);
            }
            return normalizedPath + _seperatorString + childName;
        }

        private sealed class Entry : IEntry
        {
            public Entry(ICoordinationEntry entry)
            {
                Path = entry.Path;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Value = entry.Value;
                Children = entry.Childs;
            }

            public string Path { get; }

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public IReadOnlyList<byte> Value { get; }

            public IReadOnlyCollection<string> Children { get; }
        }
    }
}
