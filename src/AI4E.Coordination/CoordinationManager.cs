using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination.Caching;
using AI4E.Coordination.Session;
using AI4E.Utils;
using AI4E.Utils.Memory;
using AI4E.Utils.Memory.Compatibility;
using AI4E.Utils.Processing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    public sealed class CoordinationManager<TAddress> : ICoordinationManager
    {
        private readonly IServiceScope _serviceScope;
        private readonly ICoordinationSessionOwner _sessionOwner;
        private readonly ISessionManager _sessionManager;
        private readonly ICoordinationCacheManager _cacheManager;
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly ILogger<CoordinationManager<TAddress>> _logger;

        private readonly CoordinationManagerOptions _options;
        private readonly AsyncProcess _updateSessionProcess;
        private readonly AsyncProcess _sessionCleanupProcess;

        internal CoordinationManager(
            IServiceScope serviceScope,
            ICoordinationSessionOwner sessionOwner,
            ISessionManager sessionManager,
            ICoordinationCacheManager cacheManager,

            IDateTimeProvider dateTimeProvider,
            IOptions<CoordinationManagerOptions> optionsAccessor,
            ILogger<CoordinationManager<TAddress>> logger = null)
        {
            if (serviceScope == null)
                throw new ArgumentNullException(nameof(serviceScope));

            if (sessionOwner == null)
                throw new ArgumentNullException(nameof(sessionOwner));

            if (sessionManager == null)
                throw new ArgumentNullException(nameof(sessionManager));

            if (cacheManager == null)
                throw new ArgumentNullException(nameof(cacheManager));

            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _serviceScope = serviceScope;
            _sessionOwner = sessionOwner;
            _sessionManager = sessionManager;
            _cacheManager = cacheManager;
            _dateTimeProvider = dateTimeProvider;
            _logger = logger;

            _options = optionsAccessor.Value ?? new CoordinationManagerOptions();
            _updateSessionProcess = new AsyncProcess(UpdateSessionProcess, start: true);
            _sessionCleanupProcess = new AsyncProcess(SessionCleanupProcess, start: true);
        }

        #region Session management

        public ValueTask<CoordinationSession> GetSessionAsync(CancellationToken cancellation)
        {
            return _sessionOwner.GetSessionAsync(cancellation);
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

        private async Task UpdateSessionAsync(CoordinationSession session, CancellationToken cancellation)
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

        private async Task CleanupSessionAsync(CoordinationSession session, CancellationToken cancellation)
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
        }

        public async ValueTask<IEntry> CreateAsync(
            CoordinationEntryPath path,
            ReadOnlyMemory<byte> value,
            EntryCreationModes modes = EntryCreationModes.Default,
            CancellationToken cancellation = default)
        {
            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var (entry, created) = await TryCreateAsync(path,
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

            return entry;
        }

        public async ValueTask<IEntry> GetOrCreateAsync(
            CoordinationEntryPath path,
            ReadOnlyMemory<byte> value,
            EntryCreationModes modes = EntryCreationModes.Default,
            CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (modes < 0 || modes > EntryCreationModes.Ephemeral)
                throw new ArgumentOutOfRangeException(nameof(modes), $"The argument must be one or a combination of the values defined in '{nameof(EntryCreationModes)}'.");

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session))
                throw new SessionTerminatedException();

            var (entry, _) = await TryCreateAsync(path,
                                                  value,
                                                  modes,
                                                  session,
                                                  cancellation);

            return entry;
        }

        private async ValueTask<(IEntry entry, bool created)> TryCreateAsync(
            CoordinationEntryPath path,
            ReadOnlyMemory<byte> value,
            EntryCreationModes modes,
            CoordinationSession session,
            CancellationToken cancellation)
        {
            var cacheEntry = await _cacheManager.GetCacheEntryAsync(path.ToString(), cancellation);

            if (cacheEntry.TryGetValue(out var cacheEntryValue) && cacheEntryValue.IsExisting && !cacheEntryValue.Value.IsEmpty)
            {
                return (Entry.FromRawValue(this, path, cacheEntryValue.Value), created: false);
            }

            if (path.IsRoot)
            {
                using (var lockedEntry = await cacheEntry.LockAsync(LockType.Exclusive, cancellation))
                {
                    if (lockedEntry.IsExisting && !lockedEntry.Value.IsEmpty)
                    {
                        return (Entry.FromRawValue(this, path, lockedEntry.Value), created: false);
                    }

                    var resultBuilder = new EntryBuilder(this, path, _dateTimeProvider) { Value = value };
                    var result = resultBuilder.ToEntry();

                    lockedEntry.CreateOrUpdate(result.ToRawValue());

                    return (result, created: true);
                }
            }

            var parentPath = path.GetParentPath();
            var parentCacheEntry = await _cacheManager.GetCacheEntryAsync(parentPath.ToString(), cancellation);
            while (true)
            {
                var parentCacheEntryValue = await parentCacheEntry.GetValueAsync(cancellation);

                if (!parentCacheEntryValue.IsExisting || parentCacheEntryValue.Value.IsEmpty)
                {
                    await TryCreateAsync(parentPath, ReadOnlyMemory<byte>.Empty, EntryCreationModes.Default, session, cancellation);
                }

                using (var lockedParentEntry = await parentCacheEntry.LockAsync(LockType.Exclusive, cancellation))
                {
                    if (!lockedParentEntry.IsExisting || lockedParentEntry.Value.IsEmpty)
                    {
                        continue;
                    }

                    var parent = Entry.FromRawValue(this, parentPath, lockedParentEntry.Value);
                    var parentBuilder = new EntryBuilder(parent, _dateTimeProvider);
                    parentBuilder.Children.Add(path.Segments.Last());
                    parent = parentBuilder.ToEntry();
                    lockedParentEntry.CreateOrUpdate(parent.ToRawValue());
                    await lockedParentEntry.FlushAsync(cancellation);

                    // TODO: This shared lots of code with the branch "path.IsRoot" above
                    using (var lockedEntry = await cacheEntry.LockAsync(LockType.Exclusive, cancellation))
                    {
                        if (lockedEntry.IsExisting && !lockedEntry.Value.IsEmpty)
                        {
                            return (Entry.FromRawValue(this, path, lockedEntry.Value), created: false);
                        }

                        var resultBuilder = new EntryBuilder(this, path, _dateTimeProvider) { Value = value };
                        var result = resultBuilder.ToEntry();

                        lockedEntry.CreateOrUpdate(result.ToRawValue());

                        return (result, created: true);
                    }
                }
            }

        }

        public async ValueTask<IEntry> GetAsync(
            CoordinationEntryPath path,
            CancellationToken cancellation = default)
        {
            var cacheEntry = await _cacheManager.GetCacheEntryAsync(path.ToString(), cancellation);

            if (cacheEntry.TryGetValue(out var value) && value.IsExisting && !value.Value.IsEmpty)
            {
                return Entry.FromRawValue(this, path, value.Value);
            }

            // This operation is called for each child of an entry, when iterating the child collection.
            // We are in the case that the requested entry cannot be found. 
            // If this is part of the parents child collection, we clean this up now.
            await UpdateParentOfDeletedEntry(path, cancellation);

            return null;
        }

        public async ValueTask<int> SetValueAsync(
            CoordinationEntryPath path,
            ReadOnlyMemory<byte> value,
            int version = 0,
            CancellationToken cancellation = default)
        {
            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            var cacheEntry = await _cacheManager.GetCacheEntryAsync(path.ToString(), cancellation);

            // If we have the entry in the cache and is non-existing => Fail fast
            if (cacheEntry.TryGetValue(out var cacheEntryValue) && (!cacheEntryValue.IsExisting || cacheEntryValue.Value.IsEmpty))
            {
                throw new EntryNotFoundException(path);
            }

            using (var lockedEntry = await cacheEntry.LockAsync(LockType.Exclusive, cancellation))
            {
                if (!lockedEntry.IsExisting || lockedEntry.Value.IsEmpty)
                {
                    throw new EntryNotFoundException(path);
                }

                var entry = Entry.FromRawValue(this, path, lockedEntry.Value);
                if (version != default && entry.Version != version)
                {
                    return entry.Version;
                }

                var entryBuilder = new EntryBuilder(entry, _dateTimeProvider) { Value = value };
                entry = entryBuilder.ToEntry();
                lockedEntry.CreateOrUpdate(entry.ToRawValue());

                return version;
            }
        }

        public async ValueTask<int> DeleteAsync(
            CoordinationEntryPath path,
            int version = 0,
            bool recursive = false,
            CancellationToken cancellation = default)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (version < 0)
                throw new ArgumentOutOfRangeException(nameof(version));

            var session = await GetSessionAsync(cancellation);

            if (!await _sessionManager.IsAliveAsync(session, cancellation))
                throw new SessionTerminatedException();

            _logger?.LogTrace($"[{await GetSessionAsync(cancellation)}] Deleting entry '{path.EscapedPath.ConvertToString()}'.");

            var key = path.ToString();
            var cacheEntry = await _cacheManager.GetCacheEntryAsync(key, cancellation);

            // If we have the entry in the cache and is non-existing, we are done
            if (cacheEntry.TryGetValue(out var cacheEntryValue) && (!cacheEntryValue.IsExisting || cacheEntryValue.Value.IsEmpty))
            {
                return 0;
            }

            // There is no parent entry.
            if (path.IsRoot)
            {
                using (var lockedCacheEntry = await cacheEntry.LockAsync(LockType.Exclusive, cancellation))
                {
                    if (!lockedCacheEntry.IsExisting || lockedCacheEntry.Value.IsEmpty)
                    {
                        return 0;
                    }

                    var entry = Entry.FromRawValue(this, path, lockedCacheEntry.Value);
                    var entryBuilder = new EntryBuilder(entry, _dateTimeProvider);

                    if (!await DeleteCoreAsync(entryBuilder, session, version, recursive, cancellation))
                    {
                        return entry.Version;
                    }

                    lockedCacheEntry.Delete();
                }
            }
            else
            {
                var parentPath = path.GetParentPath();
                var parentCacheEntry = await _cacheManager.GetCacheEntryAsync(parentPath.ToString(), cancellation);

                // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                if (parentCacheEntry.TryGetValue(out var parentCacheEntryValue) && (!parentCacheEntryValue.IsExisting || parentCacheEntryValue.Value.IsEmpty))
                {
                    return 0;
                }

                using (var lockedParentEntry = await parentCacheEntry.LockAsync(LockType.Exclusive, cancellation))
                {
                    // The parent was deleted concurrently. => The parent may only be deleted if all childs were deleted => Our entry does not exist any more.
                    if (!lockedParentEntry.IsExisting || lockedParentEntry.Value.IsEmpty)
                    {
                        return 0;
                    }

                    using (var lockedCacheEntry = await cacheEntry.LockAsync(LockType.Exclusive, cancellation))
                    {
                        if (!lockedCacheEntry.IsExisting || lockedCacheEntry.Value.IsEmpty)
                        {
                            return 0;
                        }

                        var entry = Entry.FromRawValue(this, path, lockedCacheEntry.Value);
                        var entryBuilder = new EntryBuilder(entry, _dateTimeProvider);

                        if (!await DeleteCoreAsync(entryBuilder, session, version, recursive, cancellation))
                        {
                            return entry.Version;
                        }

                        lockedCacheEntry.Delete();
                    }

                    var parentEntry = Entry.FromRawValue(this, parentPath, lockedParentEntry.Value);
                    var parentEntryBuilder = new EntryBuilder(parentEntry, _dateTimeProvider);
                    parentEntryBuilder.Children.Remove(path.Segments.Last());
                    parentEntry = parentEntryBuilder.ToEntry();

                    lockedParentEntry.CreateOrUpdate(parentEntry.ToRawValue());
                }
            }

            // TODO: Ephemeral entries

            //if (childEntryBuilder.EphemeralOwner != null)
            //{
            //    await _sessionManager.RemoveSessionEntryAsync((CoordinationSession)childEntryBuilder.EphemeralOwner, childPath, cancellation);
            //}

            return version;
        }

        // Deleted an entry without checking the input params and without locking the dispose lock.
        // The operation performs a recursive operation if the recursive parameter is true, throws an exception otherwise if there are child entries present.
        // The operation ensured consistency by locking the entry.
        // Return values:
        // entry: null if the delete operation succeeded or if the entry is not present
        // deleted: true, if the operation succeeded, false otherwise. Check the entry result in this case.
        private async Task<bool> DeleteCoreAsync(
            IEntryBuilder entryBuilder,
            CoordinationSession session,
            int version,
            bool recursive,
            CancellationToken cancellation)
        {
            if (version != default && entryBuilder.Version != version)
            {
                return false;
            }

            // Check whether there are child entries
            // It is important that all coordination manager instances handle the recursive operation in the same oder
            // (they must process all children in the exact same order) to prevent dead-lock situations.
            foreach (var childName in entryBuilder.Children)
            {
                bool deleted;
                // Recursively delete all child entries. 
                // The delete operation is not required to remove the child name entry in the parent entry, as the parent entry is to be deleted anyway.
                // In the case that we cannot proceed (our session terminates f.e.), we do not guarantee that the child names collection is strongly consistent anyway.

                // First load the child entry.
                var childPath = entryBuilder.Path.GetChildPath(childName);
                var childCacheEntry = await _cacheManager.GetCacheEntryAsync(childPath.ToString(), cancellation);

                // Check whether child actually exists before locking
                // The child-names collection is not guaranteed to be strongly consistent.
                if (childCacheEntry.TryGetValue(out var childCacheEntryValue) && (!childCacheEntryValue.IsExisting || childCacheEntryValue.Value.IsEmpty))
                {
                    continue;
                }

                using (var lockedChildEntry = await childCacheEntry.LockAsync(LockType.Exclusive, cancellation))
                {
                    if (!lockedChildEntry.IsExisting || lockedChildEntry.Value.IsEmpty)
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

                    var childEntry = Entry.FromRawValue(this, childPath, lockedChildEntry.Value);
                    var childEntryBuilder = new EntryBuilder(childEntry, _dateTimeProvider);

                    deleted = await DeleteCoreAsync(childEntryBuilder, session, version: default, recursive: true, cancellation);
                    // As we did not specify a version, the call must succeed.
                    Assert(deleted);

                    if (!deleted)
                    {
                        throw new Exception(); // TODO
                    }

                    lockedChildEntry.Delete();
                }

                // TODO: Ephemeral entries

                //if (childEntryBuilder.EphemeralOwner != null)
                //{
                //    await _sessionManager.RemoveSessionEntryAsync((CoordinationSession)childEntryBuilder.EphemeralOwner, childPath, cancellation);
                //}
            }

            return true;
        }

        #region Helpers

        private async ValueTask UpdateParentOfDeletedEntry(CoordinationEntryPath path, CancellationToken cancellation)
        {
            // The entry is the root node.
            if (path.IsRoot)
            {
                return;
            }

            var parentPath = path.GetParentPath();
            var childName = path.Segments.Last();

            var parentCacheEntry = await _cacheManager.GetCacheEntryAsync(parentPath.ToString(), cancellation);
            var parentCacheEntryValue = await parentCacheEntry.GetValueAsync(cancellation);

            // The parent does not exist.
            if (!parentCacheEntryValue.IsExisting || parentCacheEntryValue.Value.IsEmpty)
            {
                return;
            }

            var parent = Entry.FromRawValue(this, parentPath, parentCacheEntryValue.Value);

            // The parent's child collection does not contain the entry.
            if (!parent.Children.Contains(childName))
            {
                return;
            }

            var session = await GetSessionAsync(cancellation);

            using (var lockedParentEntry = await parentCacheEntry.LockAsync(LockType.Exclusive, cancellation))
            {
                // The parent does not exist.
                if (!lockedParentEntry.IsExisting || lockedParentEntry.Value.IsEmpty)
                {
                    return;
                }

                parent = Entry.FromRawValue(this, parentPath, lockedParentEntry.Value);

                // The parent's child collection does not contain the entry.
                if (!parent.Children.Contains(childName))
                {
                    return;
                }

                // As we own the write-lock now, the child collection cannot be changed concurrently.
                // Check if the child is alive. We MUST NOT delete the child entry in our collection when the child is still alive or alive again.
                var cacheEntry = await _cacheManager.GetCacheEntryAsync(path.ToString(), cancellation);
                var cacheEntryValue = await cacheEntry.GetValueAsync(cancellation);

                // The child is alive.
                if (cacheEntryValue.IsExisting && !cacheEntryValue.Value.IsEmpty)
                {
                    return;
                }

                var parentBuilder = new EntryBuilder(parent, _dateTimeProvider);
                parentBuilder.Children.Remove(childName);
                parent = parentBuilder.ToEntry();

                lockedParentEntry.CreateOrUpdate(parent.ToRawValue());
            }
        }

        #endregion

        #region Entry

        private sealed class Entry : IEntry
        {
            public Entry(
                ICoordinationManager coordinationManager,
                CoordinationEntryPath path,
                int version,
                DateTime creationTime,
                DateTime lastWriteTime,
                ReadOnlyMemory<byte> value,
                IReadOnlyList<CoordinationEntryPathSegment> children)
            {
                CoordinationManager = coordinationManager;
                Path = path;
                Version = version;
                CreationTime = creationTime;
                LastWriteTime = lastWriteTime;
                Value = value;
                Children = children as ImmutableList<CoordinationEntryPathSegment>
                    ?? children?.ToImmutableList()
                    ?? ImmutableList<CoordinationEntryPathSegment>.Empty;
            }

            public Entry(IEntryBuilder builder)
            {
                Path = builder.Path;
                Version = builder.Version;
                CreationTime = builder.CreationTime;
                LastWriteTime = builder.LastWriteTime;
                Value = builder.Value;
                Children = builder.Children.ToImmutableList();
                CoordinationManager = builder.CoordinationManager;
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

            public static Entry FromRawValue(ICoordinationManager coordinationManager, CoordinationEntryPath path, ReadOnlyMemory<byte> rawValue)
            {
                Assert(coordinationManager != null);

                var spanReader = new BinarySpanReader(rawValue.Span, ByteOrder.LittleEndian);
                var version = spanReader.ReadInt32();
                var creationTime = new DateTime(spanReader.ReadInt64());
                var lastWriteTime = new DateTime(spanReader.ReadInt64());

                var childrenCount = spanReader.ReadInt32();
                var childrenBuilder = ImmutableList.CreateBuilder<CoordinationEntryPathSegment>();

                for (var i = 0; i < childrenCount; i++)
                {
                    var escapedChildSegment = spanReader.ReadString();
                    var childSegment = CoordinationEntryPathSegment.FromEscapedSegment(escapedChildSegment.AsMemory());
                    childrenBuilder.Add(childSegment);
                }

                var value = rawValue.Slice(spanReader.Length);

                return new Entry(coordinationManager, path, version, creationTime, lastWriteTime, value, childrenBuilder.ToImmutable());
            }

            public ReadOnlyMemory<byte> ToRawValue()
            {
                var resultLength = 4 + // Version
                                   8 + // CreationTime
                                   8 + // LastWriteTime
                                   4; // ChildrenCount

                for (var i = 0; i < Children.Count; i++)
                {
                    var escapedChildSegment = Children[i].EscapedSegment;
                    resultLength += 4; // String length
                    resultLength += Encoding.UTF8.GetByteCount(escapedChildSegment.Span); // UTF8 encoded length
                }

                var result = new byte[resultLength];

                var spanWriter = new BinarySpanWriter(result.AsSpan(), ByteOrder.LittleEndian);
                spanWriter.WriteInt32(Version);
                spanWriter.WriteInt64(CreationTime.Ticks);
                spanWriter.WriteInt64(LastWriteTime.Ticks);
                spanWriter.WriteInt32(Children.Count);

                for (var i = 0; i < Children.Count; i++)
                {
                    var escapedChildSegment = Children[i].EscapedSegment;
                    spanWriter.Write(escapedChildSegment.Span, lengthPrefix: true);
                }

                Assert(spanWriter.Length == result.Length);

                return result.AsMemory();
            }
        }

        private sealed class EntryBuilder : IEntryBuilder
        {
            private ReadOnlyMemory<byte> _value;
            private readonly int _initialVersion;
            private bool _touched;
            private readonly IDateTimeProvider _dateTimeProvider;

            public EntryBuilder(
                ICoordinationManager coordinationManager,
                CoordinationEntryPath path,
                IDateTimeProvider dateTimeProvider)
            {
                if (dateTimeProvider == null)
                    throw new ArgumentNullException(nameof(dateTimeProvider));

                _dateTimeProvider = dateTimeProvider;

                CoordinationManager = coordinationManager;
                Path = path;
                CoordinationManager = coordinationManager;
                Path = path;
                _initialVersion = 0;
                _touched = true;
                CreationTime = _dateTimeProvider.GetCurrentTime();
                LastWriteTime = _dateTimeProvider.GetCurrentTime();
                _value = ReadOnlyMemory<byte>.Empty;
                Children = new EntryBuilderChildCollection(this);
            }

            public EntryBuilder(
                IEntry entry,
                IDateTimeProvider dateTimeProvider)
            {
                if (dateTimeProvider == null)
                    throw new ArgumentNullException(nameof(dateTimeProvider));

                _dateTimeProvider = dateTimeProvider;

                CoordinationManager = entry.CoordinationManager;
                Path = entry.Path;
                _initialVersion = entry.Version;
                _touched = false;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                _value = entry.Value;
                Children = new EntryBuilderChildCollection(this, entry.Children);
            }

            public ICoordinationManager CoordinationManager { get; }

            public CoordinationEntryPathSegment Name => Path.Segments.LastOrDefault();

            public CoordinationEntryPath Path { get; }

            public CoordinationEntryPath ParentPath => Path.GetParentPath();

            public int Version => _touched ? _initialVersion + 1 : _initialVersion;

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; private set; }

            public ReadOnlyMemory<byte> Value
            {
                get => _value;
                set
                {
                    _value = value;
                    Touch();
                }
            }

            public IList<CoordinationEntryPathSegment> Children { get; }

            public Entry ToEntry()
            {
                return new Entry(this);
            }

            IEntry IEntryBuilder.ToEntry()
            {
                return ToEntry();
            }

            private void Touch()
            {
                _touched = true;
                LastWriteTime = _dateTimeProvider.GetCurrentTime();
            }

            private sealed class EntryBuilderChildCollection : IList<CoordinationEntryPathSegment>
            {
                private readonly EntryBuilder _owner;
                private readonly List<CoordinationEntryPathSegment> _data;

                public EntryBuilderChildCollection(EntryBuilder owner)
                {
                    _owner = owner;
                    _data = new List<CoordinationEntryPathSegment>();
                }

                public EntryBuilderChildCollection(EntryBuilder owner, IEnumerable<CoordinationEntryPathSegment> children)
                {
                    _owner = owner;
                    _data = new List<CoordinationEntryPathSegment>(children);
                }

                public int Count => _data.Count;

                public bool IsReadOnly => false;

                public CoordinationEntryPathSegment this[int index]
                {
                    get => _data[index];
                    set
                    {
                        if (value == default)
                            throw new ArgumentDefaultException(nameof(value));

                        _data[index] = value;
                        _owner.Touch();
                    }
                }

                public int IndexOf(CoordinationEntryPathSegment item)
                {
                    return _data.IndexOf(item);
                }

                public bool Contains(CoordinationEntryPathSegment item)
                {
                    return _data.Contains(item);
                }

                public void CopyTo(CoordinationEntryPathSegment[] array, int arrayIndex)
                {
                    _data.CopyTo(array, arrayIndex);
                }

                public void Add(CoordinationEntryPathSegment item)
                {
                    if (item == default)
                        throw new ArgumentDefaultException(nameof(item));

                    _data.Add(item);
                    _owner.Touch();
                }

                public bool Remove(CoordinationEntryPathSegment item)
                {
                    if (item == default)
                        throw new ArgumentDefaultException(nameof(item));

                    var result = _data.Remove(item);

                    if (result)
                    {
                        _owner.Touch();
                    }

                    return result;
                }

                public void Insert(int index, CoordinationEntryPathSegment item)
                {
                    if (item == default)
                        throw new ArgumentDefaultException(nameof(item));

                    _data.Insert(index, item);
                    _owner.Touch();
                }

                public void RemoveAt(int index)
                {
                    _data.RemoveAt(index);
                    _owner.Touch();
                }

                public void Clear()
                {
                    _data.Clear();
                    _owner.Touch();
                }

                public IEnumerator<CoordinationEntryPathSegment> GetEnumerator()
                {
                    return _data.GetEnumerator();
                }

                IEnumerator IEnumerable.GetEnumerator()
                {
                    return _data.GetEnumerator();
                }
            }


        }

        #endregion
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
