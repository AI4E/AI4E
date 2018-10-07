using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Proxying;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debug
{
    internal class CoordinationManagerSkeleton : IDisposable
    {
        private readonly ICoordinationManager _coordinationManager;

        public CoordinationManagerSkeleton(ICoordinationManagerFactory coordinationManagerFactory)
        {
            if (coordinationManagerFactory == null)
                throw new ArgumentNullException(nameof(coordinationManagerFactory));

            _coordinationManager = coordinationManagerFactory.CreateCoordinationManager();

            Assert(_coordinationManager != null);
        }

        public async Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.CreateAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, modes, cancellation);

            if (result == null)
                return null;

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetOrCreateAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, modes, cancellation);

            if (result == null)
                return null;

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), cancellation);

            if (result == null)
                return null;

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public Task<int> SetValueAsync(string path, byte[] value, int version = 0, CancellationToken cancellation = default)
        {
            return _coordinationManager.SetValueAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), value, version, cancellation).AsTask();
        }

        public Task<int> DeleteAsync(string path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            return _coordinationManager.DeleteAsync(CoordinationEntryPath.FromEscapedPath(path.AsMemory()), version, recursive, cancellation).AsTask();
        }

        public async Task<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            return (await _coordinationManager.GetSessionAsync(cancellation)).ToString();
        }

        #region Disposal

        public void Dispose()
        {
            _coordinationManager.Dispose();
        }

        #endregion

        [Serializable]
        internal /*private*/ sealed class Entry : IEntry
        {
            [NonSerialized] // TODO: Proxy is currently not serializable. This is a bug in the proxying infrastructure
            private /*readonly*/ IProxy<CoordinationManagerSkeleton> _proxy;

            [NonSerialized]
            private ICoordinationManager _coordinationManager;

            public ICoordinationManager CoordinationManager => _coordinationManager;

            internal void SetCoordinationManagerStub(ICoordinationManager coordinationManager, IProxy<CoordinationManagerSkeleton> proxy)
            {
                _coordinationManager = coordinationManager;
                _proxy = proxy;
            }

            private readonly string[] _children; // Escaped child names
            private readonly byte[] _value;
            private readonly string _path; // Escaped path

            public Entry(IProxy<CoordinationManagerSkeleton> proxy, IEntry entry)
            {
                Assert(proxy != null);
                Assert(entry != null);

                _proxy = proxy;
                _path = entry.Path.EscapedPath.ConvertToString();
                _value = entry.Value.ToArray();
                _children = entry.Children.Select(p => p.EscapedSegment.ConvertToString()).ToArray();

                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
            }

            public CoordinationEntryPath Path => CoordinationEntryPath.FromEscapedPath(_path.AsMemory());

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public ReadOnlyMemory<byte> Value => _value;

            public IReadOnlyList<CoordinationEntryPathSegment> Children => _children.Select(p => CoordinationEntryPathSegment.FromEscapedSegment(p.AsMemory())).ToImmutableList();

            public CoordinationEntryPathSegment Name => Path.Segments.LastOrDefault();

            public CoordinationEntryPath ParentPath => Path.GetParentPath();
        }
    }
}
