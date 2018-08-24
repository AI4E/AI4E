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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
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

        private static readonly ImmutableArray<byte> _emptyValue = ImmutableArray<byte>.Empty;
        private static readonly TimeSpan _leaseLength =
#if DEBUG
        TimeSpan.FromSeconds(300);
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

        private readonly ConcurrentDictionary<string, CacheEntry> _cache;
        private readonly WaitDirectory<(string session, string entry)> _readLockWaitDirectory;
        private readonly WaitDirectory<(string session, string entry)> _writeLockWaitDirectory;

        private readonly AsyncProcess _updateSessionProcess;
        private readonly AsyncProcess _sessionCleanupProcess;
        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper<(string session, IPhysicalEndPoint<TAddress> physicalEndPoint)> _initializationHelper;
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

            _cache = new ConcurrentDictionary<string, CacheEntry>();
            _readLockWaitDirectory = new WaitDirectory<(string session, string entry)>();
            _writeLockWaitDirectory = new WaitDirectory<(string session, string entry)>();

            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess);
            _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess);
            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper<(string session, IPhysicalEndPoint<TAddress> physicalEndPoint)>(InitializeInternalAsync);
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
                    var (messageType, entry, session) = DecodeMessage(message);

                    Task.Run(() => HandleMessageAsync(message, messageType, entry, session, cancellation)).HandleExceptions();
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"[{await GetSessionAsync()}] Failure while decoding received message.");
                }
            }
        }

        private async Task HandleMessageAsync(IMessage message, MessageType messageType, string entry, string session, CancellationToken cancellation)
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
                        var path = CoordinationEntryPath.FromEscapedPath(entry.AsMemory()); // TODO: https://github.com/AI4E/AI4E/issues/15
                        await InvalidateCacheEntryAsync(path, cancellation);
                    }
                    break;

                case MessageType.ReleasedReadLock:
                    _readLockWaitDirectory.Notify((session, entry));

                    break;

                case MessageType.ReleasedWriteLock:
                    _writeLockWaitDirectory.Notify((session, entry));

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

        private async Task InvalidateCacheEntryAsync(string entry, string session, CancellationToken cancellation)
        {
            if (session == await GetSessionAsync(cancellation))
            {
                var path = CoordinationEntryPath.FromEscapedPath(entry.AsMemory()); // TODO: https://github.com/AI4E/AI4E/issues/15
                await InvalidateCacheEntryAsync(path, cancellation);
            }
            else
            {
                // The session is the read-lock owner (It caches the entry currently)
                var message = EncodeMessage(MessageType.InvalidateCacheEntry, entry, session);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task NotifyReadLockReleasedAsync(string entry, CancellationToken cancellation = default)
        {
            var sessions = await _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await GetSessionAsync(cancellation);

            foreach (var session in sessions)
            {
                if (session == localSession)
                {
                    _readLockWaitDirectory.Notify((localSession, entry));

                    continue;
                }

                // The session is the former read-lock owner.
                var message = EncodeMessage(MessageType.ReleasedReadLock, entry, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task NotifyWriteLockReleasedAsync(string entry, CancellationToken cancellation = default)
        {
            var sessions = await _sessionManager.GetSessionsAsync(cancellation);
            var localSession = await GetSessionAsync(cancellation);

            foreach (var session in sessions)
            {
                if (session == localSession)
                {
                    _writeLockWaitDirectory.Notify((localSession, entry));

                    continue;
                }

                // The session is the former write-lock owner.
                var message = EncodeMessage(MessageType.ReleasedWriteLock, entry, localSession);

                await SendMessageAsync(session, message, cancellation);
            }
        }

        private async Task SendMessageAsync(string session, Message message, CancellationToken cancellation)
        {
            var remoteAddress = SessionHelper.GetAddressFromSession(session, _addressConversion);

            Assert(remoteAddress != null);

            var physicalEndPoint = GetSessionEndPoint(session);

            try
            {
                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
            catch (SocketException) { }
            catch (IOException) { } // The remote session terminated or we just cannot transmit to it.

        }

        private IPhysicalEndPoint<TAddress> GetSessionEndPoint(string session)
        {
            Assert(session != null);

            var multiplexName = GetMultiplexEndPointName(session);

            var result = _endPointMultiplexer.GetPhysicalEndPoint(multiplexName);

            Assert(result != null);

            return result;
        }

        private static string GetMultiplexEndPointName(string session)
        {
            return "coord/" + session;
        }

        private (MessageType messageType, string entry, string session) DecodeMessage(IMessage message)
        {
            Assert(message != null);

            var messageType = default(MessageType);
            var session = default(string);
            var entry = default(string);

            using (var frameStream = message.PopFrame().OpenStream())
            using (var binaryReader = new BinaryReader(frameStream))
            {
                messageType = (MessageType)binaryReader.ReadByte();

                var entryLength = binaryReader.ReadInt32();
                var entryBytes = binaryReader.ReadBytes(entryLength);
                entry = Encoding.UTF8.GetString(entryBytes);

                var sessionLength = binaryReader.ReadInt32();
                var sessionBytes = binaryReader.ReadBytes(sessionLength);
                session = Encoding.UTF8.GetString(sessionBytes);
            }

            return (messageType, entry, session);
        }

        private Message EncodeMessage(MessageType messageType, string entry, string session)
        {
            var message = new Message();

            EncodeMessage(message, messageType, entry, session);

            return message;
        }

        private void EncodeMessage(IMessage message, MessageType messageType, string entry, string session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.ReleasedWriteLock);

            using (var frameStream = message.PushFrame().OpenStream())
            using (var binaryWriter = new BinaryWriter(frameStream))
            {
                binaryWriter.Write((byte)messageType);

                var entryBytes = Encoding.UTF8.GetBytes(entry);
                binaryWriter.Write(entryBytes.Length);
                binaryWriter.Write(entryBytes);

                var sessionBytes = Encoding.UTF8.GetBytes(session);
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
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
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

        private async Task UpdateSessionAsync(string session, CancellationToken cancellation)
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

        public async ValueTask<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            var (session, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return session;
        }

        private async Task<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation = default)
        {
            var (_, physicalEndPoint) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return physicalEndPoint;
        }

        private async Task<(string session, IPhysicalEndPoint<TAddress> physicalEndPoint)> InitializeInternalAsync(CancellationToken cancellation)
        {
            string session;

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
            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15

            var comparandVersion = 0;
            if (TryGetCacheEntry(escapedPath, out var cacheEntry))
            {
                if (cacheEntry.IsValid)
                {
                    var result = cacheEntry.Entry;
                    Assert(result != null);
                    return result;
                }

                comparandVersion = cacheEntry.Version;
            }

            return await LoadEntryAsync(path, comparandVersion, cancellation);
        }

        private async Task<IStoredEntry> LoadEntryAsync(CoordinationEntryPath path, int comparandVersion, CancellationToken cancellation)
        {
            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15

            var entry = await _storage.GetEntryAsync(escapedPath, cancellation);

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


                var (entry, created) = await CreateInternalAsync(path,
                                                                 value,
                                                                 session,
                                                                 modes,
                                                                 releaseLock: true,
                                                                 combinedCancellationSource.Token);

                // There is already an entry present.
                if (!created)
                {
                    throw new DuplicateEntryException(path.EscapedPath.ConvertToString());
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

                var (entry, _) = await CreateInternalAsync(path,
                                                           value,
                                                           session,
                                                           modes,
                                                           releaseLock: true,
                                                           combinedCancellationSource.Token);

                Assert(entry != null);

                await AddToCacheAsync(entry,
                                      comparandVersion: default,
                                      cancellation);

                return new Entry(this, entry);
            }
        }

        private async ValueTask<(IStoredEntry entry, bool created)> CreateInternalAsync(CoordinationEntryPath path,
                                                                                        ReadOnlyMemory<byte> value,
                                                                                        string session,
                                                                                        EntryCreationModes modes,
                                                                                        bool releaseLock,
                                                                                        CancellationToken cancellation)
        {
            Assert(modes >= 0 && modes <= EntryCreationModes.Ephemeral);

            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
            var entry = _storedEntryManager.Create(escapedPath, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value.Span);
            var parent = !path.IsRoot ? await _storage.GetEntryAsync(path.GetParentPath(), cancellation) : null;

            Assert(entry != null);

            try
            {
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.AddSessionEntryAsync(session, escapedPath, cancellation);
                }

                try
                {
                    // This is not the root node.
                    if (!path.IsRoot)
                    {
                        parent = await EnsureOwningParentWriteLock(parent, path.GetParentPath(), session, cancellation);

                        Assert(parent != null);

                        if (parent.EphemeralOwner != null)
                        {
                            throw new InvalidOperationException($"Unable to create the entry. The parent entry is an ephemeral node and is not allowed to have child entries.");
                        }

                        var name = path.Segments.Last().EscapedSegment.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
                        var result = await _storage.UpdateEntryAsync(parent, _storedEntryManager.AddChild(parent, name), cancellation);

                        // We are holding the exclusive lock => No one else can alter the entry.
                        // The only exception is that out session terminates.
                        if (!AreVersionEqual(result, parent))
                        {
                            throw new SessionTerminatedException();
                        }
                    }

                    var watch = new Stopwatch();
                    watch.Start();
                    var x = await CreateCoreAsync(entry, releaseLock, cancellation);
                    watch.Stop();

                    Console.WriteLine($"---> CreateCoreAsync took {watch.ElapsedMilliseconds}ms");

                    return x;
                }
                catch (SessionTerminatedException) { throw; }
                catch
                {
                    // This is not the root node and the parent node was found. 
                    // We did not successfully create the entry.
                    if (!path.IsRoot && entry != null)
                    {
                        await RemoveChildEntryAsync(parent, path.Segments.Last(), cancellation: default);
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
            catch
            {
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.RemoveSessionEntryAsync(session, escapedPath, cancellation);
                }

                throw;
            }
        }

        private async Task<IStoredEntry> EnsureOwningParentWriteLock(IStoredEntry parent, CoordinationEntryPath parentPath, string session, CancellationToken cancellation)
        {
            while (true)
            {
                // Try to aquire the write lock
                parent = await AcquireWriteLockAsync(parent, cancellation);

                // Unable to acquire write lock. Parent does not exist.
                if (parent == null)
                {
                    bool created;

                    (parent, created) = await CreateInternalAsync(parentPath, ReadOnlyMemory<byte>.Empty, session, EntryCreationModes.Default, releaseLock: false, cancellation);

                    // The parent does already exist.
                    if (!created)
                    {
                        continue;
                    }
                }

                Assert(parent != null);
                Assert(parent.WriteLock == session);

                // The parent does exist now and we own the write lock.
                return parent;
            }
        }

        private async Task<(IStoredEntry entry, bool created)> CreateCoreAsync(IStoredEntry entry, bool releaseLock, CancellationToken cancellation)
        {
            Assert(entry != null);

            var created = false;
            IStoredEntry result;

            try
            {
                var comparand = await _storage.UpdateEntryAsync(comparand: null, entry, cancellation);

                // There is already an entry present
                if (comparand != null)
                {
                    (result, created) = (comparand, false);
                }
                else
                {
                    (result, created) = (entry, true);
                }
            }
            finally
            {
                // If we created the entry successfully, we own the write lock and must unlock now.
                // If an entry was not created, we must not release the write lock (we do not own it).
                if (releaseLock && created)
                {
                    await ReleaseWriteLockAsync(entry);
                }
            }

            return (result, created);
        }

        private async Task<IStoredEntry> RemoveChildEntryAsync(IStoredEntry entry, CoordinationEntryPathSegment child, CancellationToken cancellation)
        {
            Assert(child != null);

            try
            {
                entry = await AcquireWriteLockAsync(entry, cancellation);

                if (entry == null)
                {
                    return null;
                }

                var childPath = CoordinationEntryPath.FromEscapedPath(entry.Path.AsMemory()).GetChildPath(child); // TODO: https://github.com/AI4E/AI4E/issues/15
                var childEntry = await _storage.GetEntryAsync(childPath, cancellation);

                if (childEntry == null)
                {
                    var childName = child.EscapedSegment.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
                    var result = await _storage.UpdateEntryAsync(entry, _storedEntryManager.RemoveChild(entry, childName), cancellation);

                    // We are holding the exclusive lock => No one else can alter the entry.
                    // The only exception is that out session terminates.
                    if (!AreVersionEqual(result, entry))
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

        public async ValueTask<int> DeleteAsync(CoordinationEntryPath path, int version, bool recursive, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Deleting entry '{escapedPath}'.");

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(escapedPath, combinedCancellationSource.Token);
                var parent = default(IStoredEntry);

                if (!path.IsRoot)
                {
                    var parentPath = path.GetParentPath().EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
                    parent = await _storage.GetEntryAsync(parentPath, combinedCancellationSource.Token);

                    // The parent does not exist. The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                    if (parent == null)
                    {
                        return 0;
                    }
                }

                var isEphemeral = entry == null ? false : entry.EphemeralOwner != null;

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
                            var result = await _storage.UpdateEntryAsync(entry, _storedEntryManager.Remove(entry), combinedCancellationSource.Token);

                            // We are holding the exclusive lock => No one else can alter the entry.
                            // The only exception is that out session terminates.
                            if (!AreVersionEqual(result, entry))
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
                                var name = path.Segments.Last().EscapedSegment.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
                                var result = await _storage.UpdateEntryAsync(parent, _storedEntryManager.RemoveChild(parent, name), combinedCancellationSource.Token);

                                // We are holding the exclusive lock => No one else can alter the parent.
                                // The only exception is that out session terminates.
                                if (!AreVersionEqual(result, parent))
                                {
                                    throw new SessionTerminatedException();
                                }
                            }
                        }
                        finally
                        {
                            if (isEphemeral)
                            {
                                await _sessionManager.RemoveSessionEntryAsync(session, escapedPath, combinedCancellationSource.Token);
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

        public async ValueTask<int> SetValueAsync(CoordinationEntryPath path, ReadOnlyMemory<byte> value, int version, CancellationToken cancellation = default)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(escapedPath, combinedCancellationSource.Token);

                try
                {
                    entry = await AcquireWriteLockAsync(entry, cancellation);

                    if (entry == null)
                    {
                        throw new EntryNotFoundException(escapedPath);
                    }

                    if (version != default && entry.Version != version)
                    {
                        return entry.Version;
                    }

                    var result = await _storage.UpdateEntryAsync(entry, _storedEntryManager.SetValue(entry, value.Span), combinedCancellationSource.Token);

                    // We are holding the exclusive lock => No one else can alter the entry.
                    // The only exception is that out session terminates.
                    if (!AreVersionEqual(entry, result))
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

        #region Cache

        private bool TryGetCacheEntry(string normalizedPath, out CacheEntry cacheEntry)
        {
            return _cache.TryGetValue(normalizedPath, out cacheEntry);
        }

        private void UpdateCacheEntry(string normalizedPath, Func<CacheEntry> createFunc, Func<CacheEntry, CacheEntry> updateFunc)
        {
            bool writeOk;

            do
            {
                if (!_cache.TryGetValue(normalizedPath, out var current))
                {
                    current = null;
                }

                var start = current;

                if (start == null)
                {
                    var desired = createFunc();

                    writeOk = _cache.TryAdd(normalizedPath, desired);
                }
                else
                {
                    var desired = updateFunc(start);

                    // Nothing to change in the cache. We are done.
                    if (start == desired)
                    {
                        break;
                    }

                    writeOk = _cache.TryUpdate(normalizedPath, desired, start);
                }
            }
            while (!writeOk);
        }

        private void UpdateCacheEntry(string normalizedPath, int comparandVersion, IStoredEntry entry)
        {
            UpdateCacheEntry(normalizedPath,
                             createFunc: () => new CacheEntry(normalizedPath, entry),
                             updateFunc: currentEntry => currentEntry.Update(entry, comparandVersion));
        }

        private async Task<IStoredEntry> AddToCacheAsync(IStoredEntry entry, int comparandVersion, CancellationToken cancellation)
        {
            Assert(entry != null);
            var normalizedPath = entry.Path;

            try
            {
                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    var entryPath = CoordinationEntryPath.FromEscapedPath(entry.Path.AsMemory()); // TODO: https://github.com/AI4E/AI4E/issues/15

                    if (!entryPath.IsRoot)
                    {
                        var parentPath = entryPath.GetParentPath();
                        var child = entryPath.Segments.Last();
                        var childName = child.EscapedSegment.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15

                        var parent = await _storage.GetEntryAsync(parentPath, cancellation);

                        if (parent != null && parent.Childs.Contains(childName))
                        {
                            await RemoveChildEntryAsync(parent, child, cancellation);
                        }
                    }
                }

                UpdateCacheEntry(normalizedPath, comparandVersion, entry);

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

            var escapedPath = path.EscapedPath.ConvertToString(); // TODO: https://github.com/AI4E/AI4E/issues/15
            var session = await GetSessionAsync(cancellation);

            UpdateCacheEntry(escapedPath,
                            createFunc: () => new CacheEntry(escapedPath),
                            updateFunc: currentEntry => currentEntry.Invalidate());

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Invalidating cache entry '{escapedPath}'.");

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(escapedPath, combinedCancellationSource.Token);

                await ReleaseReadLockAsync(entry);
            }
        }

        #endregion

        #region Locking

        private async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Acquiring read-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = await WaitForWriteLockReleaseAsync(entry, cancellation);

                // The entry was deleted (concurrently).
                if (start == null)
                {
                    return null;
                }

                Assert(start.WriteLock == null);

                desired = _storedEntryManager.AcquireReadLock(start, session);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (start != entry);

            Assert(entry != null);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Acquired read-lock for entry '{entry.Path}'.");

            return desired;
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync();

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Releasing read-lock for entry '{entry.Path}'.");
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

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation: _disposeHelper.DisposalRequested);
            }
            while (start != entry);

            Assert(entry != null);

            NotifyReadLockReleasedAsync(entry.Path).HandleExceptions(_logger);
            _logger?.LogTrace($"[{await GetSessionAsync()}] Released read-lock for entry '{entry.Path}'.");

            return desired;
        }

        private async Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Acquiring write-lock for entry '{entry.Path}'.");
            }

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

                desired = _storedEntryManager.AcquireWriteLock(start, session);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (entry != start);

            entry = desired;

            Assert(entry != null);

            _logger?.LogTrace($"[{await GetSessionAsync()}] Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

            try
            {
                entry = await WaitForReadLocksReleaseAsync(entry, cancellation);

                // We hold the write lock. No-one can alter the entry except our session is terminated. But this will cause WaitForReadLocksReleaseAsync to throw.
                Assert(entry != null);

                _logger?.LogTrace($"[{await GetSessionAsync()}] Acquired write-lock for entry '{entry.Path}'.");

                return entry;
            }
            catch
            {
                await ReleaseWriteLockAsync(entry);
                throw;
            }
        }

        private async Task<IStoredEntry> ReleaseWriteLockAsync(IStoredEntry entry)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync();

            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Releasing write-lock for entry '{entry.Path}'.");
            }

            do
            {
                start = entry;

                // The entry was deleted (concurrently) or the session does not own the write lock.
                if (start == null || start.WriteLock != session)
                {
                    return start;
                }

                desired = _storedEntryManager.ReleaseWriteLock(start);

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation: _disposeHelper.DisposalRequested);
            }
            while (entry != start);

            if (entry != null)
            {
                NotifyWriteLockReleasedAsync(entry.Path).HandleExceptions(_logger);
                _logger?.LogTrace($"[{await GetSessionAsync()}] Released write-lock for entry '{entry.Path}'.");
            }

            return desired;
        }

        private async Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry != null)
            {
                _logger?.LogTrace($"[{await GetSessionAsync()}] Waiting for write-lock release for entry '{entry.Path}'.");
            }

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

                var path = entry.Path;


                async Task<bool> Predicate(CancellationToken c)
                {
                    entry = await _storage.GetEntryAsync(path, c);
                    return entry == null || entry.WriteLock == null;
                }

                Task Release(CancellationToken c)
                {
                    return _writeLockWaitDirectory.WaitForNotification((writeLock, path), c);
                }

                var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation);

                try

                {
                    var lockRelease = SpinWaitAsync(Predicate, Release, combinedCancellationSource.Token);
                    var sessionTermination = _sessionManager.WaitForTerminationAsync(writeLock);
                    var completed = await (Task.WhenAny(sessionTermination, lockRelease).WithCancellation(cancellation));

                    if (completed == sessionTermination)
                    {
                        await CleanupLocksOnSessionTermination(path, writeLock, cancellation);
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

        ///// <summary>
        ///// Asynchronously waits for the specified session to terminate and releases all locks held by the session in regards to the entry with the specified path.
        ///// </summary>
        ///// <param name="key">The path to the entry the session holds locks of.</param>
        ///// <param name="session">The session thats termination shall be awaited.</param>
        ///// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        ///// <returns>A task representing the asynchronous operation.</returns>
        ///// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        ///// <exception cref="SessionTerminatedException">Thrown if <paramref name="session"/> is the local session and the session terminated before the operation is canceled.</exception>
        //private async Task WaitForSessionTermination(string key, string session, CancellationToken cancellation)
        //{
        //    await _sessionManager.WaitForTerminationAsync(session, cancellation);

        //    await CleanupLocksOnSessionTermination(key, session, cancellation);
        //}

        private async Task CleanupLocksOnSessionTermination(string key, string session, CancellationToken cancellation)
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

            IStoredEntry entry = await _storage.GetEntryAsync(key, cancellation),
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
                    desired = _storedEntryManager.ReleaseWriteLock(desired);
                }

                if (entry.ReadLocks.Contains(session))
                {
                    desired = _storedEntryManager.ReleaseReadLock(desired, session);
                }

                if (AreVersionEqual(start, desired))
                {
                    return;
                }

                entry = await _storage.UpdateEntryAsync(start, desired, cancellation);
            }
            while (start != entry);
        }

        private async Task CleanupSessionAsync(string session, CancellationToken cancellation)
        {
            _logger?.LogInformation($"[{await GetSessionAsync()}] Cleaning up session '{session}'.");

            var entries = await _sessionManager.GetEntriesAsync(session, cancellation);

            await Task.WhenAll(entries.Select(async entry =>
            {
                await DeleteAsync(CoordinationEntryPath.FromEscapedPath(entry.AsMemory()), version: default, recursive: false, cancellation); // TODO: https://github.com/AI4E/AI4E/issues/15
                await _sessionManager.RemoveSessionEntryAsync(session, entry, cancellation);
            }));

            await _sessionManager.EndSessionAsync(session, cancellation);
        }

        /// <summary>
        /// Asynchronously waits for a single read lock to be released.
        /// </summary>
        /// <param name="path">The path to the entry that the specified session holds a read lock of.</param>
        /// <param name="session">The session that holds the read lock.</param>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <exception cref="OperationCanceledException">Thrown if the operation was canceled.</exception>
        private async Task WaitForReadLockRelease(string path, string session, CancellationToken cancellation)
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
                return _readLockWaitDirectory.WaitForNotification((session, path), c);
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

                Path = CoordinationEntryPath.FromEscapedPath(entry.Path.AsMemory()); // TODO: https://github.com/AI4E/AI4E/issues/15
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Value = entry.Value;
                Children = entry.Childs.Select(p => CoordinationEntryPathSegment.FromEscapedSegment(p.AsMemory())).ToImmutableList(); // TODO: https://github.com/AI4E/AI4E/issues/15
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

        private sealed class CacheEntry
        {
            public CacheEntry(string path)
            {
                Assert(path != null);

                Path = path;
                Entry = default;
                Version = 1;
                IsValid = false;
            }

            public CacheEntry(string path, IStoredEntry entry)
            {
                Assert(path != null);
                Assert(entry != null);

                Path = path;
                Entry = entry;
                Version = 1;
                IsValid = true;
            }

            private CacheEntry(string path, IStoredEntry entry, bool isValid, int version)
            {
                Assert(path != null);
                Assert(isValid, entry != null);

                Path = path;
                Entry = entry;
                Version = version;
                IsValid = isValid;
            }

            public string Path { get; }

            public bool IsValid { get; }

            public IStoredEntry Entry { get; }

            public int Version { get; }

            public CacheEntry Invalidate()
            {
                return new CacheEntry(Path, null, isValid: false, Version + 1);
            }

            public CacheEntry Update(IStoredEntry entry, int version)
            {
                if (version != Version ||
                    entry == null ||
                    IsValid && Entry != null && Entry.StorageVersion > entry.StorageVersion)
                {
                    return this;
                }

                return new CacheEntry(Path, entry, isValid: true, version);
            }
        }

        private sealed class WaitDirectory<TKey>
        {
            private readonly Dictionary<TKey, (TaskCompletionSource<object> tcs, int refCount)> _entries;

            public WaitDirectory()
            {
                _entries = new Dictionary<TKey, (TaskCompletionSource<object> tcs, int refCount)>();
            }

            public Task WaitForNotification(TKey key, CancellationToken cancellation)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                if (cancellation.IsCancellationRequested)
                    return Task.FromCanceled(cancellation);

                TaskCompletionSource<object> tcs;

                lock (_entries)
                {
                    var refCount = 0;

                    if (_entries.TryGetValue(key, out var entry))
                    {
                        tcs = entry.tcs;
                        refCount = entry.refCount;
                    }
                    else
                    {
                        tcs = new TaskCompletionSource<object>();
                    }

                    refCount++;

                    _entries[key] = (tcs, refCount);
                }

                cancellation.Register(() =>
                {
                    lock (_entries)
                    {
                        if (!_entries.TryGetValue(key, out var entry))
                        {
                            return;
                        }

                        Assert(entry.refCount >= 1);

                        if (entry.refCount == 1)
                        {
                            _entries.Remove(key);
                        }
                        else
                        {
                            _entries[key] = (entry.tcs, entry.refCount - 1);
                        }
                    }
                });

                return tcs.Task.WithCancellation(cancellation);
            }

            public void Notify(TKey key)
            {
                if (key == null)
                    throw new ArgumentNullException(nameof(key));

                TaskCompletionSource<object> tcs;

                lock (_entries)
                {
                    if (!_entries.TryGetValue(key, out var entry))
                    {
                        return;
                    }

                    tcs = entry.tcs;
                    _entries.Remove(key);

                }

                tcs.SetResult(null);
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
