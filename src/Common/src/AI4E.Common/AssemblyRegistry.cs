/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2020 Andreas Truetschel and contributors.
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
using System.Collections.Immutable;
using System.Reflection;
using System.Runtime.Loader;
using System.Threading;
using AI4E.Utils;

namespace AI4E
{
    /// <summary>
    /// Manages a set of assemblies.
    /// </summary>
    public sealed class AssemblyRegistry : IAssemblyRegistry
    {
        private readonly IServiceProvider _serviceProvider;

        private readonly object _mutex = new object();
        private ImmutableDictionary<Assembly, AssemblyContext> _assemblies;
        private AssemblySource? _assemblySource;

        #region C'tor

        /// <summary>
        /// Creates a new instance of the <see cref="AssemblyRegistry"/> type.
        /// </summary>
        public AssemblyRegistry(IServiceProvider serviceProvider)
        {
            if (serviceProvider is null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _assemblies = ImmutableDictionary.Create<Assembly, AssemblyContext>(AssemblyByDisplayNameComparer.Instance);
            _serviceProvider = serviceProvider;
        }

        #endregion

        IAssemblySource IAssemblyRegistry.AssemblySource => AssemblySource;

        AssemblySource AssemblySource
        {
            get
            {
                var assemblySource = Volatile.Read(ref _assemblySource);

                if (assemblySource != null)
                {
                    return assemblySource;
                }

                lock (_mutex)
                {
                    assemblySource = _assemblySource ??= new AssemblySource(_assemblies, _serviceProvider);
                }

                return assemblySource;
            }
        }

        public event EventHandler? AssemblySourceChanged;

        /// <summary>
        /// Adds an assembly to the manager.
        /// </summary>
        /// <param name="assembly">The <see cref="Assembly"/> to add.</param>
        /// <param name="assemblyLoadContext">
        /// The <see cref="AssemblyLoadContext"/> that <paramref name="assembly"/> was loaded from, or <c>null</c>.
        /// </param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public void AddAssembly(
            Assembly assembly,
            AssemblyLoadContext? assemblyLoadContext = null,
            IServiceProvider? assemblyServiceProvider = null)
        {
            if (assembly is null)
                throw new ArgumentNullException(nameof(assembly));

            var context = AssemblyContext.Create(assemblyLoadContext, assemblyServiceProvider);

            if (Volatile.Read(ref _assemblies).TryGetValue(assembly, out var comparandContext)
                && comparandContext == context)
            {
                return;
            }

            lock (_mutex)
            {
                _assemblies = _assemblies.SetItem(assembly, context);
                _assemblySource = null;
            }

            NotifyAssemblySourceChanged();
        }

        public void AddAssemblies(
            IEnumerable<Assembly> assemblies,
            AssemblyLoadContext? assemblyLoadContext = null,
            IServiceProvider? assemblyServiceProvider = null)
        {
            if (assemblies is null)
                throw new ArgumentNullException(nameof(assemblies));

            var context = AssemblyContext.Create(assemblyLoadContext, assemblyServiceProvider);

            ImmutableDictionary<Assembly, AssemblyContext> current = Volatile.Read(ref _assemblies),
                start, desired;

            do
            {
                var changed = false;
                desired = start = current;

                foreach (var assembly in assemblies)
                {
                    if (assembly is null)
                    {
                        throw new ArgumentException(
                            "The collection must not contain null entries.", nameof(assemblies));
                    }

                    if (desired.TryGetValue(assembly, out var comparandContext) && comparandContext == context)
                    {
                        continue;
                    }

                    changed = true;
                    desired = desired.SetItem(assembly, context);
                }

                if (!changed)
                {
                    return;
                }

                current = CompareExchange(start, desired);
            }
            while (current != start);

            NotifyAssemblySourceChanged();
        }

        /// <summary>
        /// Removes an assembly from the manager.
        /// </summary>
        /// <param name="assembly">The assembly to remove.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="assembly"/> is <c>null</c>.</exception>
        public void RemoveAssembly(Assembly assembly)
        {
            if (assembly == null)
                throw new ArgumentNullException(nameof(assembly));

            if (!Volatile.Read(ref _assemblies).ContainsKey(assembly))
                return;

            lock (_mutex)
            {
                _assemblies = _assemblies.Remove(assembly);
                _assemblySource = null;
            }

            NotifyAssemblySourceChanged();
        }

        public void RemoveAssemblies(IEnumerable<Assembly> assemblies)
        {
            if (assemblies == null)
                throw new ArgumentNullException(nameof(assemblies));

            ImmutableDictionary<Assembly, AssemblyContext> current = Volatile.Read(ref _assemblies),
                start,
                desired;

            do
            {
                desired = start = current;

                var changed = false;

                foreach (var assembly in assemblies)
                {
                    if (assembly is null)
                    {
                        throw new ArgumentException("The collection must not contain null entries.", nameof(assemblies));
                    }

                    desired = desired.Remove(assembly);
                    changed |= start != desired;
                }

                if (!changed)
                {
                    return;
                }

                current = CompareExchange(start, desired);
            }
            while (start != current);

            NotifyAssemblySourceChanged();
        }

        public void ClearAssemblies()
        {
            var assemblies = Volatile.Read(ref _assemblies);

            if (assemblies.Count == 0)
                return;

            lock (_mutex)
            {
                _assemblies.Clear();
                _assemblySource = null;
            }

            NotifyAssemblySourceChanged();
        }

        private ImmutableDictionary<Assembly, AssemblyContext> CompareExchange(
             ImmutableDictionary<Assembly, AssemblyContext> start,
             ImmutableDictionary<Assembly, AssemblyContext> desired)
        {
            lock (_mutex)
            {
                if (start != _assemblies)
                {
                    return _assemblies;
                }

                _assemblies = desired;
                _assemblySource = null;
            }

            return start;
        }

        private void NotifyAssemblySourceChanged()
        {
            AssemblySourceChanged?.InvokeAll(this, EventArgs.Empty);
        }
    }
}
