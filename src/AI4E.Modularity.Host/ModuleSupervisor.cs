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
using System.Diagnostics;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils;
using AI4E.Utils.Async;
using AI4E.Utils.Processing;
using Microsoft.Extensions.Logging;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Host
{
    // TODO: https://github.com/AI4E/AI4E/issues/34
    //       When the host crashed and is newly swaning now, there are modules running. 
    //       How can we recognize them und use them instead of starting a new process?
    public sealed class ModuleSupervisor : IAsyncDisposable, IModuleSupervisor
    {
        private readonly IMetadataReader _metadataReader;
        private readonly IRunningModuleManager _runningModuleManager;
        private readonly ILogger<ModuleSupervisor> _logger;

        private readonly DisposableAsyncLazy<IModuleMetadata> _metadataLazy;
        private readonly AsyncDisposeHelper _disposeHelper;
        private readonly AsyncProcess _supervisorProcess;

        private readonly TimeSpan _moduleTerminateTimeout = TimeSpan.FromMilliseconds(2500); // TODO: This should be configurable

#pragma warning disable IDE0032
        private volatile ModuleSupervisorState _state;

#pragma warning restore IDE0032

        public ModuleSupervisor(DirectoryInfo directory,
                                IMetadataReader metadataReader,
                                IRunningModuleManager runningModuleManager,
                                ILogger<ModuleSupervisor> logger = null)
        {
            if (directory == null)
                throw new ArgumentNullException(nameof(directory));

            if (metadataReader == null)
                throw new ArgumentNullException(nameof(metadataReader));

            if (runningModuleManager == null)
                throw new ArgumentNullException(nameof(runningModuleManager));

            Directory = directory;
            _metadataReader = metadataReader;
            _runningModuleManager = runningModuleManager;
            _logger = logger;

            // Volatile write op (Is actually not necessary here, because the CLR enforces thread-safety.)
            _state = ModuleSupervisorState.Initializing;

            _metadataLazy = new DisposableAsyncLazy<IModuleMetadata>(
                factory: LookupMetadataAsync,
                options: DisposableAsyncLazyOptions.Autostart | DisposableAsyncLazyOptions.ExecuteOnCallingThread | DisposableAsyncLazyOptions.RetryOnFailure);

            _supervisorProcess = new AsyncProcess(SupervisorProcessRoutine, start: true);
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
            return _metadataLazy.Task.WithCancellation(cancellation);
        }

        private async Task<IModuleMetadata> LookupMetadataAsync(CancellationToken cancellation)
        {
            IModuleMetadata result;

            // TODO: Lookup metadata
            var filePath = Path.Combine(Directory.FullName, "module.json");
            using (var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, bufferSize: 4096, useAsync: true))
            {
                result = await _metadataReader.ReadMetadataAsync(fileStream, cancellation);
            }

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
                _supervisorProcess.Terminate();
                Assert(cancellation.IsCancellationRequested);

                return;
            }

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    var process = await StartProcessAsync(metadata, cancellation);

                    SetState(ModuleSupervisorState.Running);
                    _runningModuleManager.Started(metadata.Module);

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
                        _runningModuleManager.Terminated(metadata.Module);

                        throw;
                    }

                    SetState(ModuleSupervisorState.Failed);
                    _runningModuleManager.Terminated(metadata.Module);

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
            await _metadataLazy.DisposeAsync().HandleExceptionsAsync(_logger);
            await _supervisorProcess.TerminateAsync().HandleExceptionsAsync(_logger);
        }

        #endregion

        private async ValueTask<Process> StartProcessAsync(IModuleMetadata moduleMetadata,
                                                CancellationToken cancellation)
        {
            Assert(moduleMetadata != null);

            var entryAssemblyCommand = await ReplaceMetadataConstantsAsync(moduleMetadata.EntryAssemblyCommand, cancellation);
            var entryAssemblyArguments = await ReplaceMetadataConstantsAsync(moduleMetadata.EntryAssemblyArguments, cancellation);
            var processStartInfo = BuildProcessStartInfo(entryAssemblyCommand, entryAssemblyArguments);
            var process = Process.Start(processStartInfo);

            void ModuleOutputRedirect(object s, DataReceivedEventArgs e)
            {
                var data = e.Data;
                var moduleName = !string.IsNullOrWhiteSpace(moduleMetadata.Name) ? moduleMetadata.Name : moduleMetadata.Module.ToString();
                Console.WriteLine("<" + moduleName + "> " + data);
            }

            process.OutputDataReceived += ModuleOutputRedirect;
            process.ErrorDataReceived += ModuleOutputRedirect;

            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            return process;
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
                WorkingDirectory = Directory.FullName,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };
        }

        private async ValueTask<string> ReplaceMetadataConstantsAsync(string input, CancellationToken cancellation)
        {
            if (input == null)
                return null;

            var metadata = await _metadataLazy.Task.WithCancellation(cancellation);
            input = ReplaceCaseInsensitive(input, "%module%", metadata.Module.ToString());
            input = ReplaceCaseInsensitive(input, "%version%", metadata.Version.ToString());
            input = ReplaceCaseInsensitive(input, "%release%", metadata.Release.ToString());
            input = ReplaceCaseInsensitive(input, "%releasedate%", metadata.ReleaseDate.ToString("s", System.Globalization.CultureInfo.InvariantCulture));
            input = ReplaceCaseInsensitive(input, "%name%", !string.IsNullOrWhiteSpace(metadata.Name) ? metadata.Name : metadata.Module.ToString());
            input = ReplaceCaseInsensitive(input, "%description%", metadata.Description ?? string.Empty);
            input = ReplaceCaseInsensitive(input, "%author%", metadata.Author ?? string.Empty);
            input = ReplaceCaseInsensitive(input, "%hostprocessid%", Process.GetCurrentProcess().Id.ToString());
            return input;
        }

        // Based on: https://stackoverflow.com/questions/6275980/string-replace-ignoring-case/6276029
        private static string ReplaceCaseInsensitive(string input, string search, string replacement)
        {
            var result = Regex.Replace(
                input,
                Regex.Escape(search),
                replacement.Replace("$", "$$"),
                RegexOptions.IgnoreCase
            );
            return result;
        }
    }
}
