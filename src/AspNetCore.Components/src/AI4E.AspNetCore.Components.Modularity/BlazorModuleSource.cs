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
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Messaging;
using AI4E.Modularity;
using AI4E.Modularity.Metadata;
using AI4E.Utils.Async;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace AI4E.AspNetCore.Components.Modularity
{
    public sealed class BlazorModuleSource : IBlazorModuleSource
    {
        private readonly IMessageDispatcher _messageDispatcher;
        private readonly IBlazorModuleAssemblyLoader _assemblyLoader;
        private readonly ILogger<BlazorModuleSource>? _logger;
        private readonly AsyncInitializationHelper _initializationHelper;
        private ImmutableHashSet<ModuleIdentifier> _modules = ImmutableHashSet<ModuleIdentifier>.Empty;

        #region C'tor

        public BlazorModuleSource(
            IMessageDispatcher messageDispatcher,
            IBlazorModuleAssemblyLoader assemblyLoader,
            ILogger<BlazorModuleSource>? logger = null)
        {
            if (messageDispatcher is null)
                throw new ArgumentNullException(nameof(messageDispatcher));

            if (assemblyLoader is null)
                throw new ArgumentNullException(nameof(assemblyLoader));

            _messageDispatcher = messageDispatcher;
            _assemblyLoader = assemblyLoader;
            _logger = logger;
            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            var dispatchResult = await _messageDispatcher.QueryAsync<RunningModules>(cancellation);

            if (!dispatchResult.IsSuccessWithResult<RunningModules>(out var runningModules))
            {
                throw new Exception("Unable to query running modules"); // TODO
            }

            _modules = runningModules.Modules.ToImmutableHashSet();
        }

        #endregion

        #region Disposal

        // 0: false, !0: true
        private int _isDisposed = 0;

        /// <inheritdoc/>
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0)
            {
                return;
            }

            _initializationHelper.Cancel();
        }

        #endregion

        #region IBlazorModuleSource

        /// <inheritdoc/>
        public async IAsyncEnumerable<IBlazorModuleDescriptor> GetModulesAsync(
            [EnumeratorCancellation] CancellationToken cancellation)
        {
            try
            {
                await _initializationHelper.Initialization.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _isDisposed) != 0)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            if (Volatile.Read(ref _isDisposed) != 0)
            {
                throw new ObjectDisposedException(GetType().FullName);
            }

            foreach (var module in _modules)
            {
                yield return await LookupModuleAsync(module).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public event EventHandler? ModulesChanged;

        #endregion

        private void OnModulesChanged()
        {
            ModulesChanged?.Invoke(this, EventArgs.Empty);
        }

        private ValueTask<IBlazorModuleDescriptor> LookupModuleAsync(ModuleIdentifier module)
        {        
            throw new NotImplementedException();
        }

        private Task StartedAsync(ModuleIdentifier module)
        {
            _logger?.LogDebug($"Module {module.Name} is reported as running.");
            return StartedOrTerminatedAsync(module, started: true);
        }

        private Task TerminatedAsync(ModuleIdentifier module)
        {
            _logger?.LogDebug($"Module {module.Name} is reported as terminated.");
            return StartedOrTerminatedAsync(module, started: false);
        }

        private async Task StartedOrTerminatedAsync(ModuleIdentifier module, bool started)
        {
            try
            {
                await _initializationHelper.Initialization.ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            if (Volatile.Read(ref _isDisposed) != 0)
            {
                return;
            }

            ImmutableHashSet<ModuleIdentifier> current = Volatile.Read(ref _modules), start, desired;

            do
            {
                start = current;

                if (started)
                {
                    desired = start.Add(module);
                }
                else
                {
                    desired = start.Remove(module);
                }

                if (desired == start)
                {
                    return;
                }

                current = Interlocked.CompareExchange(ref _modules, desired, current);
            }
            while (start != current);

            OnModulesChanged();
        }

        private sealed class ModuleStartedEventHandler : IMessageHandler<ModuleStartedEvent>
        {
            private readonly BlazorModuleSource? _blazorModuleSource;

            public ModuleStartedEventHandler(IBlazorModuleSource blazorModuleSource)
            {
                _blazorModuleSource = blazorModuleSource as BlazorModuleSource;
            }

            public async ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary<ModuleStartedEvent> dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                if (_blazorModuleSource is null)
                    return new SuccessDispatchResult();

                try
                {
                    await _blazorModuleSource
                        .StartedAsync(dispatchData.Message.Module)
                        .ConfigureAwait(false);

                    return new SuccessDispatchResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    return new FailureDispatchResult(exc);
                }
            }

            public ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                return HandleAsync(
                    dispatchData.As<ModuleStartedEvent>(), publish, localDispatch, cancellation);
            }

            public Type MessageType => typeof(ModuleStartedEvent);
        }

        private sealed class ModuleTerminatedEventHandler : IMessageHandler<ModuleTerminatedEvent>
        {
            private readonly BlazorModuleSource? _blazorModuleSource;

            public ModuleTerminatedEventHandler(IBlazorModuleSource blazorModuleSource)
            {
                _blazorModuleSource = blazorModuleSource as BlazorModuleSource;
            }

            public async ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary<ModuleTerminatedEvent> dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                if (_blazorModuleSource is null)
                    return new SuccessDispatchResult();

                try
                {
                    await _blazorModuleSource
                        .TerminatedAsync(dispatchData.Message.Module)
                        .ConfigureAwait(false);

                    return new SuccessDispatchResult();
                }
#pragma warning disable CA1031
                catch (Exception exc)
#pragma warning restore CA1031
                {
                    return new FailureDispatchResult(exc);
                }
            }

            public ValueTask<IDispatchResult> HandleAsync(
                DispatchDataDictionary dispatchData,
                bool publish,
                bool localDispatch,
                CancellationToken cancellation)
            {
                return HandleAsync(
                    dispatchData.As<ModuleTerminatedEvent>(), publish, localDispatch, cancellation);
            }

            public Type MessageType => typeof(ModuleTerminatedEvent);
        }

        public static IMessagingBuilder Configure(IMessagingBuilder messagingBuilder)
        {
            return messagingBuilder.ConfigureMessageHandlers(ConfigureMessageHandlers);
        }

        private static void ConfigureMessageHandlers(
            IMessageHandlerRegistry handlerRegistry,
            IServiceProvider serviceProvider)
        {
            var moduleStartedEventHandlerRegistration = new MessageHandlerRegistration<ModuleStartedEvent>(
                handlerServiceProvider => ActivatorUtilities.CreateInstance<ModuleStartedEventHandler>(handlerServiceProvider));

            var moduleTerminatedEventHandlerRegistration = new MessageHandlerRegistration<ModuleTerminatedEvent>(
               handlerServiceProvider => ActivatorUtilities.CreateInstance<ModuleTerminatedEventHandler>(handlerServiceProvider));

            if (serviceProvider.GetService<IBlazorModuleSource>() is BlazorModuleSource blazorModuleSource)
            {
                handlerRegistry.Register(moduleStartedEventHandlerRegistration);
                handlerRegistry.Register(moduleTerminatedEventHandlerRegistration);
            }
        }
    }
}
