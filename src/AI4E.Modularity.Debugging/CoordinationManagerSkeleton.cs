using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Coordination;
using AI4E.Proxying;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Debugging
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

            if (result == null)
                return null;

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetOrCreateAsync(string path, byte[] value, EntryCreationModes modes = EntryCreationModes.Default, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetOrCreateAsync(path, value, modes, cancellation);

            if (result == null)
                return null;

            return new Entry((Proxy<CoordinationManagerSkeleton>)this, result);
        }

        public async Task<IEntry> GetAsync(string path, CancellationToken cancellation = default)
        {
            var result = await _coordinationManager.GetAsync(path, cancellation);

            if (result == null)
                return null;

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
        internal /*private*/ sealed class Entry : IEntry
        {
            [NonSerialized] // TODO: Proxy is currently not serializable. This is a bug in the proying infrastructure
            private /*readonly*/ IProxy<CoordinationManagerSkeleton> _proxy;

            internal void SetProxy(IProxy<CoordinationManagerSkeleton> proxy)
            {
                _proxy = proxy;
            }


            private readonly string[] _childNames;
            private readonly byte[] _value;

            public Entry(IProxy<CoordinationManagerSkeleton> proxy, IEntry entry)
            {
                Assert(proxy != null);
                Assert(entry != null);

                _proxy = proxy;

                Path = entry.Path;
                Version = entry.Version;
                CreationTime = entry.CreationTime;
                LastWriteTime = entry.LastWriteTime;
                _value = entry.Value.ToArray();
                _childNames = entry.ChildNames.ToArray();
                Name = entry.Name;
                ParentPath = entry.ParentPath;
            }

            public string Path { get; }

            public int Version { get; }

            public DateTime CreationTime { get; }

            public DateTime LastWriteTime { get; }

            public IReadOnlyList<byte> Value => _value;

            public IAsyncEnumerable<IEntry> Childs => new ChildrenEnumerable(this);

            public IReadOnlyList<string> ChildNames => _childNames;

            public string Name { get; }

            public Task<IEntry> GetParentAsync(CancellationToken cancellation)
            {
                return _proxy.ExecuteAsync(p => p.GetAsync(ParentPath, cancellation));
            }

            public string ParentPath { get; }

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
                    IEntry next;

                    do
                    {
                        string child;

                        do
                        {
                            var index = ++_currentIndex;

                            if (index >= _entry._childNames.Length)
                            {
                                _current = default;
                                return false;
                            }

                            child = _entry._childNames[index];
                        }
                        while (child == null);

                        var childFullName = EntryPathHelper.GetChildPath(_entry.Path, child, normalize: false);

                        next = await _entry._proxy.ExecuteAsync(p => p.GetAsync(childFullName, cancellationToken));
                    }
                    while (next == null);

                    _current = next;
                    return true;
                }

                public IEntry Current => _current;

                public void Dispose() { }
            }
        }
    }
}
