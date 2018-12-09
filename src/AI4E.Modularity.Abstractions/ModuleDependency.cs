using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using AI4E.Utils;

// Move to host? No: A module needs a way to look up its own dependencies.
namespace AI4E.Modularity
{
    public readonly struct ModuleDependency : IEquatable<ModuleDependency>
    {
        public static ModuleDependency Unknown { get; } = default;

        public ModuleDependency(ModuleIdentifier module, ModuleVersionRange versionRange)
        {
            if (module == ModuleIdentifier.UnknownModule)
            {
                this = Unknown;
            }
            else
            {
                Module = module;
                VersionRange = versionRange;
            }
        }

        public ModuleIdentifier Module { get; }
        public ModuleVersionRange VersionRange { get; }

        public bool Equals(ModuleDependency other)
        {
            return other.Module == Module &&
                   other.VersionRange == VersionRange;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleDependency dependency && Equals(dependency);
        }

        public override int GetHashCode()
        {
            return Module.GetHashCode() ^ VersionRange.GetHashCode();
        }

        public static bool operator ==(ModuleDependency left, ModuleDependency right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleDependency left, ModuleDependency right)
        {
            return !left.Equals(right);
        }

        [EditorBrowsable(EditorBrowsableState.Never)]
        public void Deconstruct(out ModuleIdentifier module, out ModuleVersionRange versionRange)
        {
            module = Module;
            versionRange = VersionRange;
        }
    }

    public sealed class ModuleDependencyCollection : ICollection<ModuleDependency>, IDictionary<ModuleIdentifier, ModuleVersionRange>
    {
        private readonly Dictionary<ModuleIdentifier, ModuleVersionRange> _dependencies = new Dictionary<ModuleIdentifier, ModuleVersionRange>();

        public ModuleDependencyCollection() { }

        public ModuleDependencyCollection(IEnumerable<ModuleDependency> dependencies)
        {
            if (dependencies == null)
                throw new ArgumentNullException(nameof(dependencies));

            foreach (var dependency in dependencies)
            {
                if (dependency == default)
                {
                    throw new ArgumentException("The collection must not contain default entries.");
                }

                Add(dependency);
            }
        }

        public int Count => _dependencies.Count;

        bool ICollection<ModuleDependency>.IsReadOnly => false;

        bool ICollection<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.IsReadOnly => false;

        public ModuleVersionRange this[ModuleIdentifier key]
        {
            get => _dependencies[key];
            set
            {
                if (key == default)
                    throw new ArgumentDefaultException(nameof(key));

                if (value == default)
                    throw new ArgumentDefaultException(nameof(value));
            }
        }

        ICollection<ModuleIdentifier> IDictionary<ModuleIdentifier, ModuleVersionRange>.Keys => _dependencies.Keys;

        ICollection<ModuleVersionRange> IDictionary<ModuleIdentifier, ModuleVersionRange>.Values => _dependencies.Values;

        public bool Contains(ModuleDependency item)
        {
            return _dependencies.TryGetValue(item.Module, out var versionRange) && item.VersionRange == versionRange;
        }

        public bool Contains(ModuleIdentifier module)
        {
            return _dependencies.ContainsKey(module);
        }

        bool IDictionary<ModuleIdentifier, ModuleVersionRange>.ContainsKey(ModuleIdentifier key)
        {
            return Contains(key);
        }

        bool ICollection<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.Contains(KeyValuePair<ModuleIdentifier, ModuleVersionRange> item)
        {
            if (item.Key == default || item.Value == default)
                return false;

            return Contains(new ModuleDependency(item.Key, item.Value));
        }

        public bool TryGetVersionRange(ModuleIdentifier module, out ModuleVersionRange versionRange)
        {
            return _dependencies.TryGetValue(module, out versionRange);
        }

        bool IDictionary<ModuleIdentifier, ModuleVersionRange>.TryGetValue(ModuleIdentifier key, out ModuleVersionRange value)
        {
            return TryGetVersionRange(key, out value);
        }

        void ICollection<ModuleDependency>.CopyTo(ModuleDependency[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (array.Length - arrayIndex < Count)
                throw new ArgumentException("The number of elements is greater than the available space from arrayIndex to the end of the destination array.");

            foreach (var dependency in this)
            {
                array[arrayIndex++] = dependency;
            }
        }

        void ICollection<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.CopyTo(KeyValuePair<ModuleIdentifier, ModuleVersionRange>[] array, int arrayIndex)
        {
            if (array == null)
                throw new ArgumentNullException(nameof(array));

            if (arrayIndex < 0)
                throw new ArgumentOutOfRangeException(nameof(arrayIndex));

            if (array.Length - arrayIndex < Count)
                throw new ArgumentException("The number of elements is greater than the available space from arrayIndex to the end of the destination array.");

            foreach (var dependency in this)
            {
                array[arrayIndex++] = new KeyValuePair<ModuleIdentifier, ModuleVersionRange>(dependency.Module, dependency.VersionRange);
            }
        }

        public void Add(ModuleDependency item)
        {
            if (item == default)
                throw new ArgumentDefaultException(nameof(item));

            _dependencies.Add(item.Module, item.VersionRange);
        }

        public void Add(ModuleIdentifier key, ModuleVersionRange value)
        {
            if (key == default)
                throw new ArgumentDefaultException(nameof(key));

            if (value == default)
                throw new ArgumentDefaultException(nameof(value));

            _dependencies.Add(key, value);
        }

        void ICollection<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.Add(KeyValuePair<ModuleIdentifier, ModuleVersionRange> item)
        {
            Add(item.Key, item.Value);
        }

        public bool Remove(ModuleDependency item)
        {
            if (item == default)
                throw new ArgumentDefaultException(nameof(item));

            return _dependencies.Remove(item.Module, item.VersionRange);
        }

        public bool Remove(ModuleIdentifier module)
        {
            if (module == default)
                throw new ArgumentDefaultException(nameof(module));

            return _dependencies.Remove(module);
        }

        bool IDictionary<ModuleIdentifier, ModuleVersionRange>.Remove(ModuleIdentifier key)
        {
            if (key == default)
                throw new ArgumentDefaultException(nameof(key));

            return _dependencies.Remove(key);
        }

        bool ICollection<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.Remove(KeyValuePair<ModuleIdentifier, ModuleVersionRange> item)
        {
            if (item.Key == default || item.Value == default)
                throw new ArgumentException("Neither the argument key, nor its value must be null.");

            return _dependencies.Remove(item.Key, item.Value);
        }

        public void Clear()
        {
            _dependencies.Clear();
        }

        public IEnumerator<ModuleDependency> GetEnumerator()
        {
            return _dependencies.Select(p => new ModuleDependency(p.Key, p.Value)).GetEnumerator();
        }

        IEnumerator<KeyValuePair<ModuleIdentifier, ModuleVersionRange>> IEnumerable<KeyValuePair<ModuleIdentifier, ModuleVersionRange>>.GetEnumerator()
        {
            return _dependencies.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }
    }
}
