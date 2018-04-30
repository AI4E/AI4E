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
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Processing;
using AI4E.Remoting;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class CoordinationManager<TAddress> : ICoordinationManager, IAsyncDisposable
    {
        private static readonly ImmutableArray<byte> _emptyValue = ImmutableArray<byte>.Empty;
        private static readonly TimeSpan _leaseLength =
#if DEBUG
        TimeSpan.FromSeconds(2);
#else
        TimeSpan.FromSeconds(30);
#endif
        private static readonly TimeSpan _leaseLengthHalf = new TimeSpan(_leaseLength.Ticks / 2);

        private readonly ICoordinationStorage _storage;
        private readonly IStoredEntryManager _storedEntryManager;
        private readonly ISessionManager _sessionManager;
        private readonly IEndPointMultiplexer<TAddress> _endPointMultiplexer;
        private readonly ISessionProvider _sessionProvider;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly IAddressConversion<TAddress> _addressConversion;
        private readonly ILogger<CoordinationManager<TAddress>> _logger;
        private readonly ConcurrentDictionary<string, IStoredEntry> _entries;

        private readonly AsyncProcess _updateSessionProcess;
        private readonly AsyncProcess _sessionCleanupProcess;
        private readonly AsyncProcess _receiveProcess;
        private readonly AsyncInitializationHelper<(string session, IPhysicalEndPoint<TAddress> physicalEndPoint)> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;

        public CoordinationManager(ICoordinationStorage storage,
                                   IStoredEntryManager storedEntryManager,
                                   ISessionManager sessionManager,
                                   IEndPointMultiplexer<TAddress> endPointMultiplexer,
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
            _entries = new ConcurrentDictionary<string, IStoredEntry>();

            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess);
            _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess);
            _receiveProcess = new AsyncProcess(ReceiveProcess);
            _initializationHelper = new AsyncInitializationHelper<(string session, IPhysicalEndPoint<TAddress> physicalEndPoint)>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        public async Task<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            var (session, _) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return session;
        }

        private async Task<IPhysicalEndPoint<TAddress>> GetPhysicalEndPointAsync(CancellationToken cancellation = default)
        {
            var (_, physicalEndPoint) = await _initializationHelper.Initialization.WithCancellation(cancellation);

            return physicalEndPoint;
        }

        #region Receive

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
                    _logger?.LogWarning(exc, $"Failure while decoding received message.");
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
                        _logger?.LogWarning("Received invalidate message for session that is not present.");
                    }
                    else
                    {
                        await InvalidateCacheEntryAsync(entry, cancellation);
                    }
                    break;

                case MessageType.Unknown:
                default:
                    _logger?.LogWarning("Received invalid message or message with unknown message type.");
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
                await InvalidateCacheEntryAsync(entry, cancellation);
            }
            else
            {
                var remoteAddress = SessionHelper.GetAddressFromSession(session, _addressConversion);

                Assert(remoteAddress != null);

                var message = new Message();

                EncodeMessage(message, MessageType.InvalidateCacheEntry, entry, session);

                var physicalEndPoint = await GetPhysicalEndPointAsync(cancellation);

                await physicalEndPoint.SendAsync(message, remoteAddress, cancellation);
            }
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

        private void EncodeMessage(IMessage message, MessageType messageType, string entry, string session)
        {
            Assert(message != null);
            // Modify if other message types are added
            Assert(messageType >= MessageType.InvalidateCacheEntry && messageType <= MessageType.InvalidateCacheEntry);

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
            InvalidateCacheEntry = 1
        }

        #endregion

        #region SessionManagement

        private async Task SessionCleanupProcess(CancellationToken cancellation)
        {
            var session = await GetSessionAsync(cancellation);

            _logger.LogTrace($"({session}): Started session cleanup process.");

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
                    _logger?.LogWarning(exc, $"Failure while cleaning up terminated sessions.");
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
                    _logger?.LogWarning(exc, $"Failure while updating session {session}.");
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
                var physicalEndPoint = await _endPointMultiplexer.GetMultiplexEndPointAsync("coord/session", cancellation);

                try
                {
                    await _updateSessionProcess.StartAsync(cancellation);
                    await _sessionCleanupProcess.StartAsync(cancellation);
                    await _receiveProcess.StartAsync(cancellation);
                }
                catch
                {
                    await physicalEndPoint.DisposeAsync().HandleExceptionsAsync(_logger);

                    throw;
                }

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

            await Task.WhenAll(_sessionCleanupProcess.TerminateAsync().HandleExceptionsAsync(_logger),
                               _updateSessionProcess.TerminateAsync().HandleExceptionsAsync(_logger),
                               _receiveProcess.TerminateAsync().HandleExceptionsAsync(_logger));

            if (success)
            {
                await Task.WhenAll(_sessionManager.EndSessionAsync(session).HandleExceptionsAsync(_logger),
                                   physicalEndPoint.DisposeAsync().HandleExceptionsAsync(_logger));

            }
        }

        #endregion

        #region Read entry

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var normalizedPath = EntryPathHelper.NormalizePath(path);
            var entry = await GetEntryAsync(normalizedPath, cancellation);

            if (entry == null)
            {
                return null;
            }

            return new Entry(this, entry);
        }

        private async Task<IStoredEntry> GetEntryAsync(string path, CancellationToken cancellation = default)
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

        private async Task<IStoredEntry> LoadEntryAsync(string path, CancellationToken cancellation)
        {
            var entry = await _storage.GetEntryAsync(path, cancellation);

            // We cannot cache a non existing entry.
            if (entry is null)
            {
                return null;
            }

            try
            {
                entry = await AcquireReadLockAsync(entry, cancellation);

                if (entry == null)
                {
                    var parentPath = EntryPathHelper.GetParentPath(path, out var name, normalize: false);

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

        public async Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = default, CancellationToken cancellation = default)
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

            var normalizedPath = EntryPathHelper.NormalizePath(path);
            var parentPath = EntryPathHelper.GetParentPath(normalizedPath, out var name, normalize: false);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);


                var (entry, created) = await CreateInternalAsync(normalizedPath,
                                                      parentPath,
                                                      name,
                                                      value,
                                                      session,
                                                      modes,
                                                      releaseLock: true,
                                                      combinedCancellationSource.Token);

                // There is already an entry present.
                if (!created)
                {
                    throw new DuplicateEntryException(normalizedPath);
                }

                Assert(entry != null);

                // This must not be places in the cache as we do not own a read lock for it.
                return new Entry(this, entry);
            }
        }

        public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = default, CancellationToken cancellation = default)
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

            var normalizedPath = EntryPathHelper.NormalizePath(path);
            var parentPath = EntryPathHelper.GetParentPath(normalizedPath, out var name, normalize: false);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);


                var (entry, _) = await CreateInternalAsync(normalizedPath,
                                                      parentPath,
                                                      name,
                                                      value,
                                                      session,
                                                      modes,
                                                      releaseLock: true,
                                                      combinedCancellationSource.Token);

                Assert(entry != null);

                // TODO: Throw if the entry did already exists but with other creation-modes than specified?

                // This must not be places in the cache as we do not own a read lock for it.
                return new Entry(this, entry);
            }
        }

        private async Task<(IStoredEntry entry, bool created)> CreateInternalAsync(string normalizedPath,
                                                             string parentPath,
                                                             string name,
                                                             byte[] value,
                                                             string session,
                                                             EntryCreationModes modes,
                                                             bool releaseLock,
                                                             CancellationToken cancellation)
        {
            Assert(normalizedPath != null);
            Assert(name != null);
            Assert(value != null);

            Assert(modes >= 0 && modes <= EntryCreationModes.Ephemeral);

            var entry = _storedEntryManager.Create(normalizedPath, session, (modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral, value.ToImmutableArray());
            var parent = (parentPath != null) ? await _storage.GetEntryAsync(parentPath, cancellation) : null;

            Assert(entry != null);

            try
            {
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.AddSessionEntryAsync(session, normalizedPath, cancellation);
                }

                try
                {
                    // This is not the root node.
                    if (parentPath != null)
                    {
                        parent = await EnsureOwningParentWriteLock(parent, parentPath, session, cancellation);

                        Assert(parent != null);

                        if (parent.EphemeralOwner != null)
                        {
                            throw new InvalidOperationException($"Unable to create the entry. The parent entry is an ephemeral node and is not allowed to have child entries.");
                        }

                        var result = await _storage.UpdateEntryAsync(parent, _storedEntryManager.AddChild(parent, name), cancellation);

                        // We are holding the exclusive lock => No one else can alter the entry.
                        // The only exception is that out session terminates.
                        if (!AreVersionEqual(result, parent))
                        {
                            throw new SessionTerminatedException();
                        }
                    }

                    return await CreateCoreAsync(entry, releaseLock, cancellation);
                }
                catch (SessionTerminatedException) { throw; }
                catch
                {
                    // This is not the root node and the parent node was found. 
                    // We did not successfully create the entry.
                    if (parentPath != null && entry != null)
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
            catch
            {
                if ((modes & EntryCreationModes.Ephemeral) == EntryCreationModes.Ephemeral)
                {
                    await _sessionManager.RemoveSessionEntryAsync(session, normalizedPath, cancellation);
                }

                throw;
            }
        }

        private async Task<IStoredEntry> EnsureOwningParentWriteLock(IStoredEntry parent, string normalizedPath, string session, CancellationToken cancellation)
        {
            while (true)
            {
                // Try to aquire the write lock
                parent = await AcquireWriteLockAsync(parent, cancellation);

                // Unable to acquire write lock. Parent does not exist.
                if (parent == null)
                {
                    var parentPath = EntryPathHelper.GetParentPath(normalizedPath, out var name, normalize: false);
                    bool created;

                    (parent, created) = await CreateInternalAsync(normalizedPath, parentPath, name, new byte[0], session, EntryCreationModes.Default, releaseLock: false, cancellation);

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

        private async Task<IStoredEntry> RemoveChildEntryAsync(IStoredEntry entry, string child, CancellationToken cancellation)
        {
            Assert(child != null);

            try
            {
                entry = await AcquireWriteLockAsync(entry, cancellation);

                if (entry == null)
                {
                    return null;
                }

                var childEntry = await _storage.GetEntryAsync(EntryPathHelper.GetChildPath(entry.Path, child, normalize: true), cancellation);

                if (childEntry == null)
                {
                    var result = await _storage.UpdateEntryAsync(entry, _storedEntryManager.RemoveChild(entry, child), cancellation);

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

        public async Task<int> DeleteAsync(string path, int version, bool recursive, CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var normalizedPath = EntryPathHelper.NormalizePath(path);
            var parentPath = EntryPathHelper.GetParentPath(normalizedPath, out var name, normalize: false);
            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger.LogTrace($"({session}): Deleting entry '{normalizedPath}'.");

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
                                var result = _storage.UpdateEntryAsync(parent, _storedEntryManager.RemoveChild(parent, name), combinedCancellationSource.Token);

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

            var normalizedPath = EntryPathHelper.NormalizePath(path);
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

                    var result = await _storage.UpdateEntryAsync(entry, _storedEntryManager.SetValue(entry, value.ToImmutableArray()), combinedCancellationSource.Token);

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

        // TODO: Race condition: 
        // A concurrent read and write request. 
        // 1. The read request reads the entry.
        // 2. The write request locks the entry and sends an unlock message.
        // 3. The unlock message is received and the cache entry is cleared.
        // 4. The read request succeeds and places the entry into the cache.
        public async Task InvalidateCacheEntryAsync(string path, CancellationToken cancellation)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var normalizedPath = EntryPathHelper.NormalizePath(path);
            var session = await GetSessionAsync(cancellation);

            //if (!await _sessionManager.IsAliveAsync(session, cancellation))
            //    throw new SessionTerminatedException();

            _entries.TryRemove(path, out _);

            var combinedCancellationSource = CancellationTokenSource.CreateLinkedTokenSource(cancellation, _disposeHelper.DisposalRequested);

            _logger.LogTrace($"({session}): Invalidating cache entry '{normalizedPath}'.");

            using (await _disposeHelper.ProhibitDisposalAsync(cancellation))
            {
                if (_disposeHelper.IsDisposed)
                    throw new ObjectDisposedException(GetType().FullName);

                await _initializationHelper.Initialization.WithCancellation(combinedCancellationSource.Token);

                var entry = await _storage.GetEntryAsync(normalizedPath, combinedCancellationSource.Token);

                await ReleaseReadLockAsync(entry);
            }
        }

        #region Locking

        private async Task<IStoredEntry> AcquireReadLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"({session}): Acquiring read-lock for entry '{entry.Path}'.");
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

            _logger?.LogTrace($"({session}): Acquired read-lock for entry '{entry.Path}'.");

            return desired;
        }

        private async Task<IStoredEntry> ReleaseReadLockAsync(IStoredEntry entry)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync();

            if (entry != null)
            {
                _logger?.LogTrace($"({session}): Releasing read-lock for entry '{entry.Path}'.");
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

            _logger?.LogTrace($"({session}): Released read-lock for entry '{entry.Path}'.");

            return desired;
        }

        private async Task<IStoredEntry> AcquireWriteLockAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            IStoredEntry start, desired;

            var session = await GetSessionAsync(cancellation);

            if (entry != null)
            {
                _logger?.LogTrace($"({session}): Acquiring write-lock for entry '{entry.Path}'.");
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

            _logger?.LogTrace($"({session}): Pending write-lock for entry '{entry.Path}'. Waiting for read-locks to release.");

            try
            {
                entry = await WaitForReadLocksReleaseAsync(entry, cancellation);

                // We hold the write lock. No-one can alter the entry except for our session is terminated. But this will cause WaitForReadLocksReleaseAsync to throw.
                Assert(entry != null);

                _logger?.LogTrace($"({session}): Acquired write-lock for entry '{entry.Path}'.");

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
                _logger?.LogTrace($"({session}): Releasing write-lock for entry '{entry.Path}'.");
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
                _logger?.LogTrace($"({session}): Released write-lock for entry '{entry.Path}'.");
            }

            return desired;
        }

        private async Task<IStoredEntry> WaitForWriteLockReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            if (entry != null)
            {
                _logger?.LogTrace($"Waiting for write-lock release for entry '{entry.Path}'.");
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

        private async Task<IStoredEntry> WaitForReadLocksReleaseAsync(IStoredEntry entry, CancellationToken cancellation)
        {
            Assert(entry != null);

            if (entry != null)
            {
                _logger?.LogTrace($"Waiting for read-locks release for entry '{entry.Path}'.");
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

            //await CleanupSessionAsync(session, cancellation);

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

                if (entry.WriteLock == session)
                {
                    desired = _storedEntryManager.ReleaseWriteLock(start);
                }
                else if (entry.ReadLocks.Contains(session))
                {
                    desired = _storedEntryManager.ReleaseReadLock(start, session);
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
            _logger.LogInformation($"Cleaning up session '{session}'.");

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

            await InvalidateCacheEntryAsync(path, session, cancellation);

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
            private readonly ImmutableArray<string> _children;
            private readonly CoordinationManager<TAddress> _coordinationManager;

            public Entry(CoordinationManager<TAddress> coordinationManager, IStoredEntry entry)
            {
                Assert(coordinationManager != null);
                Assert(entry != null);

                Path = entry.Path;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Value = entry.Value;
                _children = entry.Childs;
                _coordinationManager = coordinationManager;
                Children = new ChildrenEnumerable(this);
            }

            public string Path { get; }

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public IReadOnlyList<byte> Value { get; }

            public IAsyncEnumerable<IEntry> Children { get; }

            private sealed class ChildrenEnumerable : IAsyncEnumerable<IEntry>
            {
                private readonly Entry _entry;

                public ChildrenEnumerable(Entry entry)
                {
                    Assert(entry != null);

                    _entry = entry;
                }

                public IAsyncEnumerator<IEntry> GetEnumerator()
                {
                    return new ChildrenEnumerator(_entry);
                }
            }

            private sealed class ChildrenEnumerator : IAsyncEnumerator<IEntry>
            {
                private readonly Entry _entry;

                private IEntry _current = default;
                private int _currentIndex = -1;

                public ChildrenEnumerator(Entry entry)
                {
                    Assert(entry != null);

                    _entry = entry;
                }

                public async Task<bool> MoveNext(CancellationToken cancellationToken)
                {
                    string child;

                    do
                    {
                        var index = ++_currentIndex;

                        if (index >= _entry._children.Length)
                        {
                            return false;
                        }

                        child = _entry._children[index];
                    }
                    while (child == null);

                    var childFullName = EntryPathHelper.GetChildPath(_entry.Path, child, normalize: false);

                    _current = await _entry._coordinationManager.GetAsync(childFullName, cancellationToken);

                    return true;
                }

                public IEntry Current => _current;

                public void Dispose() { }
            }
        }
    }
}
