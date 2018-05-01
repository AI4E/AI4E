using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Proxying;
using static System.Diagnostics.Debug;

namespace AI4E.Routing.Debugging
{
    internal class CoordinationManagerSkeleton : IDisposable
    {
        private readonly ICoordinationManager _coordinationManager;

        public CoordinationManagerSkeleton(IProvider<ICoordinationManager> coordinationManagerProvider)
        {
            if (coordinationManagerProvider == null)
                throw new ArgumentNullException(nameof(coordinationManagerProvider));

            _coordinationManager = coordinationManagerProvider.ProvideInstance();

            Assert(_coordinationManager != null);
        }

        public async Task<IEntry> CreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.CreateAsync(path, value, modes, cancellation);

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetOrCreateAsync(path, value, modes, cancellation);

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetAsync(path, cancellation);

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public Task<int> SetValueAsync(string path, byte[] value, int version = 0, CancellationToken cancellation = default)
        {
            return _coordinationManager.SetValueAsync(path, value, version, cancellation);
        }

        public Task<int> DeleteAsync(string path, int version = 0, bool recursive = false, CancellationToken cancellation = default)
        {
            return _coordinationManager.DeleteAsync(path, version, recursive, cancellation);
        }

        public Task<string> GetSessionAsync(CancellationToken cancellation = default)
        {
            return _coordinationManager.GetSessionAsync(cancellation);
        }

        #region Disposal

        public void Dispose()
        {
            _coordinationManager.Dispose();
        }

        #endregion

        [Serializable]
        private sealed class Entry : IEntry
        {
            private readonly IProxy<CoordinationManagerSkeleton> _proxy;
            private readonly ImmutableArray<string> _children;

            public Entry(IProxy<CoordinationManagerSkeleton> proxy, IEntry entry)
            {
                Assert(proxy != null);
                Assert(entry != null);

                _proxy = proxy;

                Path = entry.Path;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                Value = entry.Value;
                _children = entry.ChildNames.ToImmutableArray();

                Children = new ChildrenEnumerable(this);
            }

            public string Path { get; }

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public IReadOnlyList<byte> Value { get; }

            public IAsyncEnumerable<IEntry> Children { get; }

            public IReadOnlyList<string> ChildNames => _children;

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

                    _current = await _entry._proxy.ExecuteAsync(p => p.GetAsync(childFullName, cancellationToken));

                    return true;
                }

                public IEntry Current => _current;

                public void Dispose() { }
            }
        }
    }
}
