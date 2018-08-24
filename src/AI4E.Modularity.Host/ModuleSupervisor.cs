using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    // TODO: When the host crashed and is newly swaning now, there are running modules. 
    //       How can we recognize them und use them instead of starting a new process?
    public sealed class ModuleSupervisor : IAsyncDisposable, IModuleSupervisor
    {
        private readonly AsyncInitializationHelper<IModuleMetadata> _initializationHelper;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly IAsyncProcess _supervisorProcess;
        private readonly IMetadataReader _metadataReader;
        private readonly ILogger<ModuleSupervisor> _logger;

        private readonly TimeSpan _moduleTerminateTimeout = TimeSpan.FromMilliseconds(2500); // TODO: This should be configurable

#pragma warning disable IDE0032
        private volatile ModuleSupervisorState _state;
#pragma warning restore IDE0032

        public ModuleSupervisor(DirectoryInfo directory,
                                IMetadataReader metadataReader,
                                ILogger<ModuleSupervisor> logger = null)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            Directory = directory;
            _metadataReader = metadataReader;
            _logger = logger;

            // Volatile write op (Is avtually not necessary here, because the CLR enforces thread-safety.)
            _state = ModuleSupervisorState.Initializing;

            _supervisorProcess = new AsyncProcess(SupervisorProcessRoutine);
            _initializationHelper = new AsyncInitializationHelper<IModuleMetadata>(InitializeInternalAsync);
            _disposeHelper = new AsyncDisposeHelper(DisposeInternalAsync);
        }

        #region IModuleSupervisor

        public DirectoryInfo Directory { get; }

        public ModuleSupervisorState State => _state; // Volatile read op.

        public event EventHandler<ModuleSupervisorState> StateChanged;

        public async Task<ModuleReleaseIdentifier> GetSupervisedModule(CancellationToken cancellation)
        {
            var metadata = await GetMetadataAsync(cancellation);

            return metadata.Release;
        }

        #endregion

        private void SetState(ModuleSupervisorState state)
        {
            Assert(state >= ModuleSupervisorState.Initializing && state <= ModuleSupervisorState.Shutdown);

            _state = state;  // Volatile write op.

            StateChanged?.Invoke(this, state);
        }

        private Task<IModuleMetadata> GetMetadataAsync(CancellationToken cancellation)
        {
            return _initializationHelper.Initialization.WithCancellation(cancellation);
        }

        private async Task<IModuleMetadata> InitializeInternalAsync(CancellationToken cancellation)
        {
            IModuleMetadata result;

            // TODO: Lookup metadata
            var filePath = Path.Combine(Directory.FullName, "module.json");
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                result = await _metadataReader.ReadMetadataAsync(fileStream, cancellation);
            }

            await _supervisorProcess.StartAsync(cancellation);

            return result;
        }

        private async Task SupervisorProcessRoutine(CancellationToken cancellation)
        {
            IModuleMetadata metadata;

            try
            {
                metadata = await GetMetadataAsync(cancellation);
            }
            catch
            {
                SetState(ModuleSupervisorState.Shutdown);
                throw;
            }

            SetState(ModuleSupervisorState.NotRunning);

            // This is a meta-module and cannot be started.
            if (string.IsNullOrWhiteSpace(metadata.EntryAssemblyCommand))
            {
#pragma warning disable CS4014
                _supervisorProcess.TerminateAsync();
#pragma warning restore CS4014
                Assert(cancellation.IsCancellationRequested);

                return;
            }

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var process = await StartProcessAsync(metadata, cancellation);

                    SetState(ModuleSupervisorState.Running);

                    try
                    {
                        await process.WaitForExitAsync(cancellation);
                    }
                    catch (OperationCanceledException)
                    {
                        // The supervisor is shutdown.
                        // => We have to terminate the process.
                        await TerminateProcessAsync(_moduleTerminateTimeout, process);

                        SetState(ModuleSupervisorState.Shutdown);

                        throw;
                    }

                    SetState(ModuleSupervisorState.Failed);

                    // The process exited unexpectedly.
                    // TODO: Log
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // TODO: Log exception
                }
            }
        }

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
            var (success, result) = await _initializationHelper.CancelAsync();
            await _supervisorProcess.TerminateAsync().HandleExceptionsAsync(_logger);
        }

        #endregion

        private Task<Process> StartProcessAsync(IModuleMetadata moduleMetadata,
                                                CancellationToken cancellation)
        {
            Assert(moduleMetadata != null);

            var entryAssemblyCommand = ReplaceMetadataConstants(moduleMetadata.EntryAssemblyCommand);
            var entryAssemblyArguments = ReplaceMetadataConstants(moduleMetadata.EntryAssemblyArguments);
            var processStartInfo = BuildProcessStartInfo(entryAssemblyCommand, entryAssemblyArguments);
            var process = Process.Start(processStartInfo);

            return Task.FromResult(process);
        }

        private static async Task TerminateProcessAsync(TimeSpan moduleTerminateTimeout, Process process)
        {
            Assert(process != null);
            Assert(moduleTerminateTimeout >= TimeSpan.Zero);

            // We try to gracefully close the process first.
            if (moduleTerminateTimeout > TimeSpan.Zero)
            {
                process.CloseMainWindow();

                var cts = new CancellationTokenSource(moduleTerminateTimeout);

                try
                {
                    await process.WaitForExitAsync(cts.Token);

                    return;
                }
                catch (OperationCanceledException) { }
            }

            process.Kill();
        }

        private ProcessStartInfo BuildProcessStartInfo(string entryAssemblyCommand, string entryAssemblyArguments)
        {
            return new ProcessStartInfo(entryAssemblyCommand, entryAssemblyArguments)
            {
                CreateNoWindow = true,
                UseShellExecute = false,
                WorkingDirectory = Directory.FullName
            };
        }

        private string ReplaceMetadataConstants(string input)
        {
            if (input == null)
                return null;

            // TODO

            return input;
        }
    }
}
