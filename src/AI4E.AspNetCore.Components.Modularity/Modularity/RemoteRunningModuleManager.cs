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
using System.Threading.Tasks;
using AI4E.Modularity;
using AI4E.Modularity.Host;
using AI4E.Utils;
using AI4E.Utils.Async;
using Microsoft.Extensions.Logging;

namespace AI4E.AspNetCore.Components.Modularity
{
    // TODO: This is copied from AI4E.Modularity.Host.RunningModuleManager
    //       Can we reduce the code duplication?
    public sealed class RemoteRunningModuleManager : IRunningModuleManager, IAsyncInitialization, IDisposable
    {
        private ImmutableList<ModuleIdentifier> _modules = ImmutableList<ModuleIdentifier>.Empty;
        private readonly object _mutex = new object();

        private readonly AsyncInitializationHelper _initializationHelper;
        private readonly IMessageDispatcher _messageDispatcher;

        public RemoteRunningModuleManager(IMessageDispatcher messageDispatcher)
        {
            if (messageDispatcher == null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            _messageDispatcher = messageDispatcher;
            _initializationHelper = new AsyncInitializationHelper(InitiallyLoadModules);
        }

        private async Task InitiallyLoadModules(CancellationToken cancellation)
        {
            var dispatchResult = await _messageDispatcher.QueryAsync<RunningModules>(cancellation);

            if (dispatchResult.IsSuccessWithResult<RunningModules>(out var runningModules))
            {
                foreach (var module in runningModules.Modules)
                {
                    Started(module);
                }
            }
        }

        // TODO: This should be internal, but we have to split the interface to do this.
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
                ModuleStarted?.InvokeAll(this, module);
            }
        }

        // TODO: This should be internal, but we have to split the interface to do this.
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
                ModuleTerminated?.InvokeAll(this, module);
            }
        }

        public IReadOnlyCollection<ModuleIdentifier> Modules => Volatile.Read(ref _modules);

        public event EventHandler<ModuleIdentifier> ModuleStarted;
        public event EventHandler<ModuleIdentifier> ModuleTerminated;

        public Task Initialization { get; }

        public void Dispose()
        {
            _initializationHelper.Cancel();
        }
    }

    [MessageHandler]
    internal sealed class RunningModuleEventHandler
    {
        private readonly IRunningModuleManager _runningModuleManager;
        private readonly ILogger<RunningModuleEventHandler> _logger;

        public RunningModuleEventHandler(
            IRunningModuleManager runningModuleManager,
            ILogger<RunningModuleEventHandler> logger = null)
        {
            if (runningModuleManager == null)
                throw new ArgumentNullException(nameof(runningModuleManager));

            _runningModuleManager = runningModuleManager;
            _logger = logger;
        }

        public void Handle(ModuleStartedEvent eventMessage)
        {
            _logger?.LogDebug($"Module {eventMessage.Module.Name} is reported as running.");
            _runningModuleManager.Started(eventMessage.Module);
        }

        public void Handle(ModuleTerminatedEvent eventMessage)
        {
            _logger?.LogDebug($"Module {eventMessage.Module.Name} is reported as terminated.");
            _runningModuleManager.Terminated(eventMessage.Module);
        }
    }
}
