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
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;
using static AI4E.Internal.DebugEx;

namespace AI4E.Coordination
{
    public sealed class CoordinationManager<TAddress> : ICoordinationManager
    {
        #region Fields

        private static readonly TimeSpan _leaseLength =
#if DEBUG
        TimeSpan.FromSeconds(30);
#else
        TimeSpan.FromSeconds(30);
#endif
        private static readonly TimeSpan _leaseLengthHalf = new TimeSpan(_leaseLength.Ticks / 2);

        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationManager<TAddress>> _logger;

        private readonly CoordinationEntryCache _cache;
        private readonly CoordinationLockManager _lockManager;

        private readonly AsyncWaitDirectory<(Session session, CoordinationEntryPath path)> _readLockWaitDirectory;
        private readonly AsyncWaitDirectory<(Session session, CoordinationEntryPath path)> _writeLockWaitDirectory;

        private readonly IAsyncProcess _updateSessionProcess;
        private readonly IAsyncProcess _sessionCleanupProcess;
        private readonly IAsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper<(Session session, IPhysicalEndPoint<TAddress> physicalEndPoint)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        #endregion

        #region C'tor

        public CoordinationManager(ICoordinationStorage storage,
                                   IStoredEntryManager storedEntryManager,
                                   ISessionManager sessionManager,
                                   IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                                   ISessionProvider sessionProvider,
                                   IDateTimeProvider dateTimeProvider,
                                   IAddressConversion<TAddress> addressConversion,
                                   ILogger<CoordinationManager<TAddress>> logger)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (sessionProvider == null)
                throw new ArgumentNullException(nameof(sessionProvider));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _sessionManager = sessionManager;
            _endPointMultiplexer = endPointMultiplexer;
            _sessionProvider = sessionProvider;
            _dateTimeProvider = dateTimeProvider;
            _addressConversion = addressConversion;
            _logger = logger;

            _cache = new CoordinationEntryCache();
            _lockManager = new CoordinationLockManager(_storage, _storedEntryManager, _cache, this, _logger);
            _readLockWaitDirectory = new AsyncWaitDirectory<(Session session, CoordinationEntryPath path)>();
            _writeLockWaitDirectory = new AsyncWaitDirectory<(Session session, CoordinationEntryPath path)>();

            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess);
            _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess);
            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper<(Session session, IPhysicalEndPoint<TAddress> physicalEndPoint)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #endregion

        #region RX/TX

        private async Task ReceiveProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var message = await ReceiveMessageAsync(cancellation);
                    var (messageType, path, session) = DecodeMessage(message);

                    Task.Run(() => HandleMessageAsync(message, messageType, path, session, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await GetSessionAsync()}] Failure while decoding received message.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, MessageType messageType, CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            switch (messageType)
            {
                case MessageType.InvalidateCacheEntry:
                    if (session != await GetSessionAsync(cancellation))
                    {
                        _logger?.LogWarning($"[{await GetSessionAsync()}] Received invalidate message for session that is not present.");
                    }
                    else
                    {
                        await InvalidateCacheEntryAsync(path, cancellation);
                    }
                    break;

                case MessageType.ReleasedReadLock:
                    _readLockWaitDirectory.Notify((session, path));

                    break;

                case MessageType.ReleasedWriteLock:
                    _writeLockWaitDirectory.Notify((session, path));

                    break;

                case MessageType.Unknown:
                default:
                    _logger?.LogWarning($"[{await GetSessionAsync()}] Received invalid message or message with unknown message type.");
                    break;
            }
        }

        private async Task<IMessage> ReceiveMessageAsync(CancellationToken cancellation)
        {
            var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation);

            return await physicalEndPoint.ReceiveAsync(cancellation);
        }

        private async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            if (session == await GetSessionAsync(cancellation))
            {
                await InvalidateCacheEntryAsync(path, cancellation);
            }
            else
            {
                // The session is the read-lock owner (It caches the entry currently)
                var message = EncodeMessage(MessageType.InvalidateCacheEntry, path, session);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            var sessions = await _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await GetSessionAsync(cancellation);

            foreach (var session in sessions)
            {
                if (session == localSession)
                {
                    _readLockWaitDirectory.Notify((localSession, path));

                    continue;
                }

                // The session is the former read-lock owner.
                var message = EncodeMessage(MessageType.ReleasedReadLock, path, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            var sessions = await _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await GetSessionAsync(cancellation);

            foreach (var session in sessions)
            {
                if (session == localSession)
                {
                    _writeLockWaitDirectory.Notify((localSession, path));

                    continue;
                }

                // The session is the former write-lock owner.
                var message = EncodeMessage(MessageType.ReleasedWriteLock, path, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task SendMessageAsync(Session session, Message message, CancellationToken cancellation)
        {
            var remoteAddress = _addressConversion.DeserializeAddress(session.PhysicalAddress.ToArray()); // TODO: This will copy everything to a new aray

            Assert(remoteAddress != null);

            var physicalEndPoint = GetSessionEndPoint(session);

            try
            {
                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch (SocketException) { }
            catch (IOException) { } // The remote session terminated or we just cannot transmit to it.

        }

        private IPhysicalEndPoint<TAddress> GetSessionEndPoint(Session session)
        {
            Assert(session != null);

            var multiplexName = GetMultiplexEndPointName(session);

            var result = _endPointMultiplexer.GetPhysicalEndPoint(multiplexName);

            Assert(result != null);

            return result;
        }

        private static string GetMultiplexEndPointName(Session session)
        {
            return "coord/" + session.ToCompactString();
        }

        private (MessageType messageType, CoordinationEntryPath path, Session session) DecodeMessage(IMessage message)
        {
            Assert(message != null);

            using (var frameStream = message.PopFrame().OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                var messageType = (MessageType)binaryReader.ReadByte();

                var escapedPath = binaryReader.ReadUtf8();
                var path = CoordinationEntryPath.FromEscapedPath(escapedPath);

                var sessionLength = binaryReader.ReadInt32();
                var sessionBytes = binaryReader.ReadBytes(sessionLength);
                var session = Session.FromChars(Encoding.UTF8.GetString(sessionBytes).AsSpan());

                return (messageType, path, session);
            }
        }

        private Message EncodeMessage(MessageType messageType, CoordinationEntryPath path, Session session)
        {
            var message = new Message();

            EncodeMessage(message, messageType, path, session);

            return message;
        }

        private void EncodeMessage(IMessage message, MessageType messageType, CoordinationEntryPath path, Session session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.ReleasedWriteLock);

            using (var frameStream = message.PushFrame().OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write((byte)messageType);

                binaryWriter.WriteUtf8(path.EscapedPath.Span);

                var sessionBytes = Encoding.UTF8.GetBytes(session.ToCompactString());
                binaryWriter.Write(sessionBytes.Length);
                binaryWriter.Write(sessionBytes);
            }
        }

        private enum MessageType : byte
        {
            Unknown = 0,
            InvalidateCacheEntry = 1,
            ReleasedReadLock = 2,
            ReleasedWriteLock = 3
        }

        #endregion

        #region SessionManagement

        private async Task SessionCleanupProcess(CancellationToken cancellation)
        {
            var session = await GetSessionAsync(cancellation);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Started session cleanup process.");

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
                catch (OperationCanceledException) when (_disposeHelper.IsDisposed)
                {
                    return; // TODO: https://github.com/AI4E/AI4E/issues/37
                }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await GetSessionAsync()}] Failure while cleaning up terminated sessions.");
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
                    _logger?.LogWarning(exc, $"[{await GetSessionAsync()}] Failure while updating session {session}.");
                }
            }
        }

        private async Task UpdateSessionAsync(Session session, CancellationToken cancellation)
        {
            Assert(session != null);

            var leaseEnd = _dateTimeProvider.GetCurrentTime() + _leaseLength;

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

        public async ValueTask<Session> GetSessionAsync(CancellationToken cancellation = default)
        {
            var (session, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return session;
        }

        private async Task<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation = default)
        {
            var (_, physicalEndPoint) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return physicalEndPoint;
        }

        private async Task<(Session session, IPhysicalEndPoint<TAddress> physicalEndPoint)> InitializeInternalAsync(CancellationToken cancellation)
        {
            Session session;

            do
            {
                session = _sessionProvider.GetSession();

                Assert(session != null);
            }
            while (!await _sessionManager.TryBeginSessionAsync(session, leaseEnd: _dateTimeProvider.GetCurrentTime() + _leaseLength, cancellation));

            try
            {
                var physicalEndPoint = GetSessionEndPoint(session);

                try
                {
                    await _updateSessionProcess.StartAsync(cancellation);
                    await _sessionCleanupProcess.StartAsync(cancellation);
                    await _receiveProcess.StartAsync(cancellation);
                }
                catch
                {
                    await physicalEndPoint.DisposeIfDisposableAsync().HandleExceptionsAsync(_logger);

                    throw;
                }

                _logger?.LogInformation($"[{session}] Initialized coordination manager.");

                return (session, physicalEndPoint);
            }
            catch
            {
                await _sessionManager.EndSessionAsync(session).HandleExceptionsAsync(_logger);

                throw;
            }
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
            var (success, (session, physicalEndPoint)) = await _initializationHelper.CancelAsync().HandleExceptionsAsync(_logger);

            if (success)
            {
                _logger?.LogInformation($"[{session}] Disposing coordination manager.");
            }
            else
            {
                _logger?.LogInformation($"Disposing coordination manager.");
            }

            await Task.WhenAll(_sessionCleanupProcess.TerminateAsync().HandleExceptionsAsync(_logger),
                               _updateSessionProcess.TerminateAsync().HandleExceptionsAsync(_logger),
                               _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger));

            if (success)
            {
                await Task.WhenAll(_sessionManager.EndSessionAsync(session).HandleExceptionsAsync(_logger),
                                   physicalEndPoint.DisposeIfDisposableAsync().HandleExceptionsAsync(_logger));

            }
        }

        #endregion

        #region Read entry

        public async ValueTask<IEntry> GetAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            var entry = await GetEntryInternalAsync(path, cancellation);

            if (entry == null)
            {
                return null;
            }

            return new Entry(this, entry);
        }

        private async ValueTask<IStoredEntry> GetEntryInternalAsync(CoordinationEntryPath path, CancellationToken cancellation = default)
        {
            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);
                return await GetEntryCoreAsync(path, combinedCancellationSource.Token);
            }
        }

        private async ValueTask<IStoredEntry> GetEntryCoreAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            var comparandVersion = 0;
            if (TryGetCacheEntry(path, out var cacheEntry))
            {
                if (cacheEntry.IsValid)
                {
                    var result = cacheEntry.Entry;
                    Assert(result != null);
                    return result;
                }

                comparandVersion = cacheEntry.CacheEntryVersion;
            }

            return await LoadEntryAsync(path, comparandVersion, cancellation);
        }

        private async Task<IStoredEntry> LoadEntryAsync(CoordinationEntryPath path, int comparandVersion, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            // We cannot cache a non existing entry.
            if (entry is null)
            {
                return null;
            }

            return await AddToCacheAsync(entry, comparandVersion, cancellation);
        }

        #endregion

        #region Create entry

        public async ValueTask<IEntry> CreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = default, CancellationToken cancellation = default)
        {
            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);


                var (entry, created) = await TryCreateInternalAsync(path,
                                                                    value,
                                                                    modes,
                                                                    session,
                                                                    combinedCancellationSource.Token);

                // There is already an entry present.
                if (!created)
                {
                    throw new DuplicateEntryException(path);
                }

                Assert(entry != null);

                await AddToCacheAsync(entry,
                                      comparandVersion: default,
                                      cancellation);

                return new Entry(this, entry);
            }
        }

        public async ValueTask<IEntry> GetOrCreateAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, EntryCreationModes modes = default, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var cachedEntry = await GetEntryCoreAsync(path, combinedCancellationSource.Token);

                if (cachedEntry != null)
                {
                    return new Entry(this, cachedEntry);
                }

                var (entry, _) = await TryCreateInternalAsync(path,
                                                              value,
                                                              modes,
                                                              session,
                                                              combinedCancellationSource.Token);

                Assert(entry != null);

                await AddToCacheAsync(entry,
                                      comparandVersion: default,
                                      cancellation);

                return new Entry(this, entry);
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
                using (var parentLockOwnership = await EnsureParentLock(parentPath, session, cancellation))
                {
                    var parent = parentLockOwnership.Entry;

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
                        await RemoveChildEntryAsync(parent, path.Segments.Last(), cancellation: default);
                        throw;
                    }
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

        private async Task<CoordinationLockManager.WriteLockReleaser> EnsureParentLock(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            CoordinationLockManager.WriteLockReleaser result = default;

            try
            {
                while (true)
                {
                    result = await _lockManager.AcquireWriteLockAsync(path, cancellation);

                    if (result.Entry != null)
                    {
                        Assert(result.Entry.WriteLock == session);
                        return result;
                    }

                    result.Dispose();

                    await TryCreateInternalAsync(path, ReadOnlyMemory<byte>.Empty, modes: default, session, cancellation);
                }
            }
            catch
            {
                result.Dispose();
                throw;
            }
        }

        private async Task<(IStoredEntry entry, bool created)> TryCreateCoreAsync(CoordinationEntryPath path,
                                                                                  ReadOnlyMemory<byte> value,
                                                                                  EntryCreationModes modes,
                                                                                  Session session,
                                                                                  CancellationToken cancellation)
        {
            var entry = _storedEntryManager.Create(path, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value.Span);

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

            using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(path, cancellation))
            {
                var comparand = await _storage.UpdateEntryAsync(entry, comparand: null, cancellation);

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
        }

        private async Task<IStoredEntry> RemoveChildEntryAsync(IStoredEntry entry, CoordinationEntryPathSegment child, CancellationToken cancellation)
        {
            Assert(child != null);

            var session = await GetSessionAsync(cancellation);

            using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(entry.Path, cancellation))
            {
                entry = lockOwnership.Entry;

                if (entry == null)
                {
                    return null;
                }

                var childPath = entry.Path.GetChildPath(child);
                var childEntry = await _storage.GetEntryAsync(childPath, cancellation);

                if (childEntry == null)
                {
                    await UpdateEntryAsync(_storedEntryManager.RemoveChild(entry, child, session), entry, cancellation);
                }

                return entry;
            }
        }

        #endregion

        #region Delete entry

        public async ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version, bool recursive, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Deleting entry '{path.EscapedPath.ConvertToString()}'.");

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                // There is a parent entry.
                if (!path.IsRoot)
                {
                    using (var parentLockOwnership = await _lockManager.AcquireWriteLockAsync(path.GetParentPath(), combinedCancellationSource.Token))
                    {
                        var parent = parentLockOwnership.Entry;

                        // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                        if (parent == null)
                        {
                            return 0;
                        }

                        using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(path, combinedCancellationSource.Token))
                        {
                            var result = await DeleteInternalAsync(lockOwnership.Entry, session, version, recursive, combinedCancellationSource.Token);

                            // The entry was already deleted.
                            if (result == 0)
                            {
                                return 0;
                            }

                            // Version conflict.
                            if (result != version)
                            {
                                return version;
                            }

                            // The entry is deleted now, because WE deleted it.
                            var name = path.Segments.Last();
                            await UpdateEntryAsync(_storedEntryManager.RemoveChild(parent, name, session), parent, combinedCancellationSource.Token);
                        }
                    }
                }

                using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(path, cancellation))
                {
                    return await DeleteInternalAsync(lockOwnership.Entry, session, version, recursive, cancellation);
                }
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
                // Recursively delete all child entries. 
                // The delete operation is not required to remove the child name entry in the parent entry, as the parent entry is  to be deleted anyway.
                // In the case that we cannot proceed (our session terminates f.e.), we do not guarantee that the child names collection is strongly consistent anyway.

                // First load the child entry.
                var childPath = entry.Path.GetChildPath(childName);
                var child = await _storage.GetEntryAsync(childPath, cancellation);

                // The child-names collection is not guaranteed to be strongly consistent.
                if (child == null)
                {
                    continue;
                }

                // Check whether we allow recursive delete operation.
                // This cannot be done upfront, 
                // as the child-names collection is not guaranteed to be strongly consistent.
                // The child names collection may contain child names but the childs are not present actually.
                // => We check for the recursive option if we find any child that is present actually.
                if (!recursive)
                {
                    throw new InvalidOperationException("An entry that contains child entries cannot be deleted.");
                }

                bool deleted;
                using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(childPath, cancellation))
                {
                    child = lockOwnership.Entry;
                    (child, deleted) = await DeleteCoreAsync(child, session, version: default, recursive: true, cancellation);
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

        public async ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version, CancellationToken cancellation = default)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                using (var lockOwnership = await _lockManager.AcquireWriteLockAsync(path, combinedCancellationSource.Token))
                {
                    var entry = lockOwnership.Entry;

                    if (entry == null)
                    {
                        throw new EntryNotFoundException(path);
                    }

                    if (version != default && entry.Version != version)
                    {
                        return entry.Version;
                    }

                    await UpdateEntryAsync(_storedEntryManager.SetValue(entry, value.Span, session), entry, combinedCancellationSource.Token);
                }
            }

            return version;
        }

        #endregion

        #region Cache

        private bool TryGetCacheEntry(CoordinationEntryPath path, out ICacheEntry cacheEntry)
        {
            return _cache.TryGetEntry(path, out cacheEntry);
        }

        [Obsolete]
        private void UpdateCacheEntry(IStoredEntry entry, int comparandVersion)
        {
            Assert(entry != null);

            _cache.UpdateEntry(entry, comparandVersion);
        }

        private void UpdateCacheEntry(ICacheEntry cacheEntry, IStoredEntry entry/*, int comparandVersion*/)
        {
            Assert(cacheEntry != null);
            Assert(entry != null);

            _cache.UpdateEntry(cacheEntry, entry);
        }

        [Obsolete]
        private async Task<IStoredEntry> AddToCacheAsync(IStoredEntry entry, int comparandVersion, CancellationToken cancellation)
        {
            Assert(entry != null);

            try
            {
                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    if (!entry.Path.IsRoot)
                    {
                        var parentPath = entry.Path.GetParentPath();
                        var child = entry.Path.Segments.Last();

                        var parent = await _storage.GetEntryAsync(parentPath, cancellation);

                        if (parent != null && parent.Children.Contains(child))
                        {
                            await RemoveChildEntryAsync(parent, child, cancellation);
                        }
                    }
                }

                _cache.UpdateEntry(entry, comparandVersion);
                return entry;
            }
            catch
            {
                await ReleaseReadLockAsync(entry);

                throw;
            }
        }

        private async Task<IStoredEntry> AddToCacheAsync(ICacheEntry cacheEntry, IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(cacheEntry != null);
            Assert(entry != null);

            try
            {
                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    if (!entry.Path.IsRoot)
                    {
                        var parentPath = entry.Path.GetParentPath();
                        var child = entry.Path.Segments.Last();

                        var parent = await _storage.GetEntryAsync(parentPath, cancellation);

                        if (parent != null && parent.Children.Contains(child))
                        {
                            await RemoveChildEntryAsync(parent, child, cancellation);
                        }
                    }
                }

                _cache.UpdateEntry(cacheEntry, entry);

                return entry;
            }
            catch
            {
                await ReleaseReadLockAsync(entry);

                throw;
            }
        }

        private async Task InvalidateCacheEntryAsync(CoordinationEntryPath path, CancellationToken cancellation)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var session = await GetSessionAsync(cancellation);

            _cache.InvalidateEntry(path);

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Invalidating cache entry '{path.EscapedPath.ConvertToString()}'.");

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(path, combinedCancellationSource.Token);

                await ReleaseReadLockAsync(entry);
            }
        }

        #endregion

        #region Locking

        private async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Stopwatch watch = null;
            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                watch = new Stopwatch();
                watch.Start();
            }

            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquiring read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = await WaitForWriteLockReleaseAsync(entry, allowWriteLock: true, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Assert(start.WriteLock == null || start.WriteLock == session);

                desired = _storedEntryManager.AcquireReadLock(start, session);

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            Assert(entry != null);

            if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
            {
                Assert(watch != null);
                watch.Stop();

                _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquired read-lock for entry '{entry.Path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
            }

            return desired;
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry, CancellationToken cancellation = default)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Releasing read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = entry;

                // The entry was deleted (concurrently).
                if (start == null || !start.ReadLocks.Contains(session))
                {
                    return null;
                }

                desired = _storedEntryManager.ReleaseReadLock(start, session);

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);

            Assert(entry != null);

            NotifyReadLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
            _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Released read-lock for entry '{entry.Path}'.");

            return desired;
        }

        private async Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, bool allowWriteLock, CancellationToken cancellation)
        {
            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Waiting for write-lock release for entry '{entry.Path}'.");
            }

            var session = await GetSessionAsync(cancellation);

            // The entry was deleted (concurrently).
            while (entry != null)
            {
                var writeLock = entry.WriteLock;

                if (writeLock == null)
                {
                    return entry;
                }

                // If (entry.WriteLock == session) we MUST wait till the lock is released
                // and acquired again in order that no concurrency conflicts may occur.
                // Because of the case that we may keep a write and read lock at the same time, this does hold true only if we aquire a write lock.
                // In this case, we could also allow this but had to synchronize the write operations locally.
                if (allowWriteLock && writeLock == session)
                {
                    return entry;
                }

                var path = entry.Path;

                async Task<bool> Predicate(CancellationToken c)
                {
                    entry = await _storage.GetEntryAsync(path, c);
                    return entry == null || entry.WriteLock == null;
                }

                Task Release(CancellationToken c)
                {
                    return _writeLockWaitDirectory.WaitForNotificationAsync(((Session)writeLock, path), c);
                }

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

                try

                {
                    var lockRelease = SpinWaitAsync(Predicate, Release, combinedCancellationSource.Token);
                    var sessionTermination = _sessionManager.WaitForTerminationAsync((Session)writeLock);
                    var completed = await (Task.WhenAny(sessionTermination, lockRelease).WithCancellation(cancellation));

                    if (completed == sessionTermination)
                    {
                        await CleanupLocksOnSessionTermination(path, (Session)writeLock, cancellation);
                        entry = await _storage.GetEntryAsync(path, cancellation);
                    }
                }
                finally
                {
                    combinedCancellationSource.Cancel();
                }
            }

            return null;
        }

        private async Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Waiting for read-locks release for entry '{entry.Path}'.");
            }

            IEnumerable<Session> readLocks = entry.ReadLocks;

            // Exclude our own read-lock (if present)
            var session = await GetSessionAsync(cancellation);
            readLocks = readLocks.Where(readLock => readLock != session);

            // Send a message to each of 'readLocks' to ask for lock release and await the end of the session or the lock release, whichever occurs first.
            await Task.WhenAll(readLocks.Select(readLock => WaitForReadLockRelease(entry.Path, readLock, cancellation)));

            // The unlock of the 'readLocks' will alter the db, so we have to read the entry again.
            entry = await _storage.GetEntryAsync(entry.Path, cancellation);

            // We are holding the write-lock => The entry must not be deleted concurrently.
            if (entry == null || entry.ReadLocks.Length != 0 && (entry.ReadLocks.Length > 1 || entry.ReadLocks.First() != session))
            {
                throw new SessionTerminatedException();
            }

            return entry;
        }

        private async Task CleanupLocksOnSessionTermination(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
#if DEBUG
            var isTerminated = !await _sessionManager.IsAliveAsync(session, cancellation);

            Assert(isTerminated);
#endif

            var localSession = await GetSessionAsync(cancellation);

            // We waited for ourself to terminate => We are terminated now.
            if (session == localSession)
            {
                throw new SessionTerminatedException();
            }

            IStoredEntry entry = await _storage.GetEntryAsync(path, cancellation),
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

                desired = start;

                if (entry.WriteLock == session)
                {
                    desired = _storedEntryManager.ReleaseWriteLock(desired, session);
                }

                if (entry.ReadLocks.Contains(session))
                {
                    desired = _storedEntryManager.ReleaseReadLock(desired, session);
                }

                if (AreVersionEqual(start, desired))
                {
                    return;
                }

                entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
            }
            while (start != entry);
        }

        private async Task CleanupSessionAsync(Session session, CancellationToken cancellation)
        {
            _logger?.LogInformation($"[{await GetSessionAsync()}] Cleaning up session '{session}'.");

            var entries = await _sessionManager.GetEntriesAsync(session, cancellation);

            await Task.WhenAll(entries.Select(async entry =>
                      {
                          await DeleteAsync(entry, version: default, recursive: false, cancellation);
                          await _sessionManager.RemoveSessionEntryAsync(session, entry, cancellation);
                      }));

            await _sessionManager.EndSessionAsync(session, cancellation);
        }

        private async Task WaitForReadLockRelease(CoordinationEntryPath path, Session session, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            if (entry == null || !entry.ReadLocks.Contains(session))
            {
                return;
            }

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
            {
                await CleanupLocksOnSessionTermination(path, session, cancellation);
                return;
            }

            async Task<bool> Predicate(CancellationToken c)
            {
                entry = await _storage.GetEntryAsync(path, c);

                if (entry != null && entry.ReadLocks.Contains(session))
                {
                    InvalidateCacheEntryAsync(path, session, c).HandleExceptions(_logger);
                    return false;
                }

                return true;
            }

            Task Release(CancellationToken c)
            {
                return _readLockWaitDirectory.WaitForNotificationAsync((session, path), c);
            }

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

            try
            {
                var lockRelease = SpinWaitAsync(Predicate, Release, combinedCancellationSource.Token);
                var sessionTermination = _sessionManager.WaitForTerminationAsync(session);
                var completed = await (Task.WhenAny(sessionTermination, lockRelease).WithCancellation(cancellation));

                if (completed == sessionTermination)
                {
                    await CleanupLocksOnSessionTermination(path, session, cancellation);
                    entry = await _storage.GetEntryAsync(path, cancellation);
                }
            }
            finally
            {
                combinedCancellationSource.Cancel();
            }

        }

        private async Task SpinWaitAsync(Func<CancellationToken, Task<bool>> predicate, Func<CancellationToken, Task> release, CancellationToken cancellation)
        {
            var timeToWait = new TimeSpan(200 * TimeSpan.TicksPerMillisecond);
            var timeToWaitMax = new TimeSpan(12800 * TimeSpan.TicksPerMillisecond);

            cancellation.ThrowIfCancellationRequested();
            var releaseX = release(cancellation); // TODO: Rename

            while (!await predicate(cancellation))
            {
                cancellation.ThrowIfCancellationRequested();

                var delay = Task.Delay(timeToWait, cancellation);

                var completed = await Task.WhenAny(delay, releaseX);

                if (completed == releaseX)
                {
                    return;
                }

                if (timeToWait < timeToWaitMax)
                    timeToWait = new TimeSpan(timeToWait.Ticks * 2);
            }
        }

        #endregion

        private async Task UpdateEntryAsync(IStoredEntry value, IStoredEntry comparand, CancellationToken cancellation)
        {
            var result = await _storage.UpdateEntryAsync(value, comparand, cancellation);

            // We are holding the exclusive lock => No one else can alter the entry.
            // The only exception is that out session terminates.
            if (!AreVersionEqual(result, comparand))
            {
                throw new SessionTerminatedException();
            }
        }

        private static bool AreVersionEqual(IStoredEntry left, IStoredEntry right)
        {
            if (left is null)
                return right is null;

            if (right is null)
                return false;

            Assert(left.Path == right.Path);

            return left.StorageVersion == right.StorageVersion;
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

        private sealed class CoordinationLockManager
        {
            private readonly ICoordinationStorage _storage;
            private readonly IStoredEntryManager _storedEntryManager;
            private readonly CoordinationEntryCache _cache;
            private readonly CoordinationManager<TAddress> _coordinationManager;
            private readonly ILogger _logger;

            public CoordinationLockManager(ICoordinationStorage storage,
                                           IStoredEntryManager storedEntryManager,
                                           CoordinationEntryCache cache,
                                           CoordinationManager<TAddress> coordinationManager,
                                           ILogger logger = null)
            {
                Assert(storage != null);
                Assert(storedEntryManager != null);
                Assert(cache != null);
                Assert(coordinationManager != null);

                _storage = storage;
                _storedEntryManager = storedEntryManager;
                _cache = cache;
                _coordinationManager = coordinationManager;
                _logger = logger;
            }

            #region TODO

            private ValueTask<Session> GetSessionAsync(CancellationToken cancellation)
            {
                return _coordinationManager.GetSessionAsync(cancellation); // TODO
            }

            private void TerminateSession()
            {

            }

            private Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                return _coordinationManager.WaitForReadLocksReleaseAsync(entry, cancellation); // TODO
            }

            private Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry,
                                                                    bool allowWriteLock,
                                                                    CancellationToken cancellation)
            {
                return _coordinationManager.WaitForWriteLockReleaseAsync(entry, allowWriteLock, cancellation); // TODO
            }

            private Task NotifyReadLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                return _coordinationManager.NotifyReadLockReleasedAsync(path, cancellation); // TODO
            }

            private Task NotifyWriteLockReleasedAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                return _coordinationManager.NotifyWriteLockReleasedAsync(path, cancellation); // TODO
            }

            #endregion

            public async Task<WriteLockReleaser> AcquireWriteLockAsync(CoordinationEntryPath path, CancellationToken cancellation)
            {
                var cacheEntry = _cache.GetEntry(path);
                IDisposable localLockReleaser = null;

                try
                {
                    // Enter local lock
                    localLockReleaser = await cacheEntry.LocalLock.LockAsync(cancellation);

                    // Enter global lock
                    var entry = await AcquireWriteLockInternalAsync(cacheEntry.Entry, cancellation);

                    try
                    {
                        // TODO: Is it necessary to update the cache here?

                        // Build the write lock releaser.
                        return new WriteLockReleaser(this, path, entry, localLockReleaser);
                    }
                    catch
                    {
                        // Release global lock on failure
                        await ReleaseWriteLockAsync(entry, cancellation: default);
                        throw;
                    }
                }
                catch
                {
                    // Release local lock on failure
                    if (localLockReleaser != null)
                    {
                        localLockReleaser.Dispose();
                    }
                    throw;
                }
            }

            private async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                Stopwatch watch = null;
                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    watch = new Stopwatch();
                    watch.Start();
                }

                IStoredEntry start, desired;

                var session = await GetSessionAsync(cancellation);

                if (entry != null)
                {
                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquiring read-lock for entry '{entry.Path}'.");
                }

                do
                {
                    start = await WaitForWriteLockReleaseAsync(entry, allowWriteLock: true, cancellation);

                    // The entry was deleted (concurrently).
                    if (start == null)
                    {
                        return null;
                    }

                    Assert(start.WriteLock == null || start.WriteLock == session);

                    desired = _storedEntryManager.AcquireReadLock(start, session);

                    entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
                }
                while (start != entry);

                Assert(entry != null);

                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    Assert(watch != null);
                    watch.Stop();

                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquired read-lock for entry '{entry.Path.EscapedPath}' in {watch.ElapsedMilliseconds}ms.");
                }

                return desired;
            }

            private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                IStoredEntry start, desired;

                var session = await GetSessionAsync(cancellation);

                if (entry != null)
                {
                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Releasing read-lock for entry '{entry.Path}'.");
                }

                do
                {
                    start = entry;

                    // The entry was deleted (concurrently).
                    if (start == null || !start.ReadLocks.Contains(session))
                    {
                        return null;
                    }

                    desired = _storedEntryManager.ReleaseReadLock(start, session);

                    entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
                }
                while (start != entry);

                Assert(entry != null);

                NotifyReadLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
                _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Released read-lock for entry '{entry.Path}'.");

                return desired;
            }

            private async Task<IStoredEntry> AcquireWriteLockInternalAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                Stopwatch watch = null;
                if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                {
                    watch = new Stopwatch();
                    watch.Start();
                }

                IStoredEntry start, desired;

                var session = await GetSessionAsync(cancellation);

                if (entry != null)
                {
                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquiring write-lock for entry '{entry.Path}'.");
                }

                do
                {
                    // WaitForWriteLockReleaseAsync updates the session in order that there is enough time to complete the write operation, without the session to terminate.
                    start = await WaitForWriteLockReleaseAsync(entry, allowWriteLock: false, cancellation);

                    // The entry was deleted (concurrently).
                    if (start == null)
                    {
                        return null;
                    }

                    Assert(start.WriteLock == null);

                    desired = _storedEntryManager.AcquireWriteLock(start, session);

                    entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
                }
                while (entry != start);

                entry = desired;

                Assert(entry != null);

                _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

                try
                {
                    entry = await WaitForReadLocksReleaseAsync(entry, cancellation);

                    // We hold the write lock. No-one can alter the entry except our session is terminated. But this will cause WaitForReadLocksReleaseAsync to throw.
                    Assert(entry != null);

                    if (_logger?.IsEnabled(LogLevel.Trace) ?? false)
                    {
                        Assert(watch != null);
                        watch.Stop();

                        _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Acquired write-lock for entry '{entry.Path}' in {watch.Elapsed.TotalSeconds}sec.");
                    }

                    return entry;
                }
                catch
                {
                    await ReleaseWriteLockAsync(entry, cancellation: default);
                    throw;
                }
            }

            private async Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
            {
                IStoredEntry start, desired;

                var session = await GetSessionAsync(cancellation);

                if (entry != null)
                {
                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Releasing write-lock for entry '{entry.Path}'.");
                }

                ICacheEntry cacheEntry;

                do
                {
                    start = entry;

                    // The entry was deleted (concurrently) or the session does not own the write lock.
                    if (start == null || start.WriteLock != session)
                    {
                        return start;
                    }

                    // As we did not invalidate the cache entry on write-lock acquirement, we have to update the cache.
                    // We must not update the cache, before we released the write lock, because it is not guaranteed, 
                    // that the write lock release is successful on the first attempt.
                    // We read the cache-entry before we release the write lock, to get the cache entry version and
                    // afterwards try to update the cache if no other session invalidated our cache entry in the meantime.
                    cacheEntry = _cache.GetEntry(entry.Path);
                    desired = _storedEntryManager.ReleaseWriteLock(start, session);
                    entry = await _storage.UpdateEntryAsync(desired, start, cancellation);
                }
                while (entry != start);

                _cache.UpdateEntry(cacheEntry, entry);

                if (entry != null)
                {
                    NotifyWriteLockReleasedAsync(entry.Path, cancellation).HandleExceptions(_logger);
                    _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Released write-lock for entry '{entry.Path}'.");
                }

                return desired;
            }

            public readonly struct WriteLockReleaser : IDisposable
            {
                private readonly CoordinationLockManager _lockManager;
                private readonly IDisposable _localLockReleaser;

                internal WriteLockReleaser(CoordinationLockManager lockManager,
                                           CoordinationEntryPath path,
                                           IStoredEntry entry,
                                           IDisposable localLockReleaser)
                {
                    _lockManager = lockManager;
                    Path = path;
                    Entry = entry;
                    _localLockReleaser = localLockReleaser;
                }

                public CoordinationEntryPath Path { get; }
                public IStoredEntry Entry { get; }

                public void Dispose()
                {
                    if (_lockManager == null) // (this == default)
                        return;

                    DisposeInternalAsync().HandleExceptions(_lockManager._logger);
                }

                private async Task DisposeInternalAsync()
                {
                    try
                    {
                        await _lockManager.ReleaseWriteLockAsync(Entry, cancellation: default);
                        _localLockReleaser.Dispose();
                    }
                    catch
                    {
                        _lockManager.TerminateSession();
                        throw;
                    }
                }
            }
        }
    }

    public sealed class CoordinationManagerFactory<TAddress> : ICoordinationManagerFactory
    {
        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IPhysicalEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationManager<TAddress>> _logger;

        public CoordinationManagerFactory(ICoordinationStorage storage,
                        IStoredEntryManager storedEntryManager,
                        ISessionManager sessionManager,
                        IPhysicalEndPointMultiplexer<TAddress> endPointMultiplexer,
                        ISessionProvider sessionProvider,
                        IDateTimeProvider dateTimeProvider,
                        IAddressConversion<TAddress> addressConversion,
                        ILogger<CoordinationManager<TAddress>> logger)
        {
            if (storage == null)
                throw new ArgumentNullException(nameof(storage));

            if (storedEntryManager == null)
                throw new ArgumentNullException(nameof(storedEntryManager));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (endPointMultiplexer == null)
                throw new ArgumentNullException(nameof(endPointMultiplexer));

            if (sessionProvider == null)
                throw new ArgumentNullException(nameof(sessionProvider));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (addressConversion == null)
                throw new ArgumentNullException(nameof(addressConversion));

            _storage = storage;
            _storedEntryManager = storedEntryManager;
            _sessionManager = sessionManager;
            _endPointMultiplexer = endPointMultiplexer;
            _sessionProvider = sessionProvider;
            _dateTimeProvider = dateTimeProvider;
            _addressConversion = addressConversion;
            _logger = logger;
        }

        public ICoordinationManager CreateCoordinationManager()
        {
            return new CoordinationManager<TAddress>(_storage,
                                                     _storedEntryManager,
                                                     _sessionManager,
                                                     _endPointMultiplexer,
                                                     _sessionProvider,
                                                     _dateTimeProvider,
                                                     _addressConversion,
                                                     _logger);
        }
    }
}
