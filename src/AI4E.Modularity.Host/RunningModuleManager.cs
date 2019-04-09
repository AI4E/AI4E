/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Threading;

namespace AI4E.Modularity.Host
{
    public sealed class RunningModuleManager : IRunningModuleManager
    {
        private ImmutableList<ModuleIdentifier> _modules = ImmutableList<ModuleIdentifier>.Empty;
        private readonly object _mutex = new object();
        private readonly IMessageDispatcher _messageDispatcher;

        public RunningModuleManager(IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
        }

        public void Started(ModuleIdentifier module)
        {
            bool added;

            lock (_mutex)
            {
                added = !_modules.Contains(module);

                if (added)
                {
                    _modules = _modules.Add(module);
                }
            }

            if (added)
            {
                try
                {
                    var moduleStarted = Volatile.Read(ref ModuleStarted);

                    if (moduleStarted != null)
                    {
                        EventInvocationHelper.Invoke(moduleStarted, d => d(this, module));
                    }
                }
                finally
                {
                    _messageDispatcher.Dispatch(new ModuleStartedEvent(module), publish: true);
                }
            }
        }

        public void Terminated(ModuleIdentifier module)
        {
            bool removed;

            lock (_mutex)
            {
                var modules = _modules;
                _modules = _modules.Remove(module);

                removed = modules != _modules;
            }

            if (removed)
            {
                try
                {
                    var moduleTerminated = Volatile.Read(ref ModuleTerminated);

                    if (moduleTerminated != null)
                    {
                        EventInvocationHelper.Invoke(moduleTerminated, d => d(this, module));
                    }
                }
                finally
                {
                    _messageDispatcher.Dispatch(new ModuleTerminatedEvent(module), publish: true);
                }
            }
        }

        public IReadOnlyCollection<ModuleIdentifier> Modules => Volatile.Read(ref _modules);

        public event EventHandler<ModuleIdentifier> ModuleStarted;
        public event EventHandler<ModuleIdentifier> ModuleTerminated;
    }
}
