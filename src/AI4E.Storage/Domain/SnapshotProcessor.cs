using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Utils;
using JsonDiffPatchDotNet;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;

namespace AI4E.Storage.Domain
{
    public sealed class SnapshotProcessor : ISnapshotProcessor, IAsyncDisposable
    {
        private static JToken StreamRoot => JToken.Parse("{}");

        #region Fields

        private readonly IStreamStore _streamStore;
        private readonly IServiceProvider _serviceProvider;

        private readonly JsonDiffPatch _differ;
        private readonly StorageOptions _options;
        private readonly IAsyncProcess _snapshotProcess;
        private readonly CancellationTokenSource _cancellationSource = new CancellationTokenSource();
        private readonly Task _initialization;
        private Task _disposal;
        private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
        private readonly object _lock = new object();

        #endregion

        #region C'tor

        public SnapshotProcessor(IStreamStore streamStore,
                                 IServiceProvider serviceProvider,
                                 IOptions<StorageOptions> optionsAccessor)
        {
            if (streamStore == null)
                throw new ArgumentNullException(nameof(streamStore));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _streamStore = streamStore;
            _serviceProvider = serviceProvider;

            _differ = new JsonDiffPatch();
            _options = optionsAccessor.Value ?? new StorageOptions();
            _snapshotProcess = new AsyncProcess(SnapshotProcess);
            _initialization = InitializeInternalAsync(_cancellationSource.Token);
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _snapshotProcess.StartAsync();
        }

        #endregion

        #region Disposal

        public Task Disposal => _disposalSource.Task;

        private async Task DisposeInternalAsync()
        {
            try
            {
                // Cancel the initialization
                _cancellationSource.Cancel();
                try
                {
                    await _initialization;
                }
                catch (OperationCanceledException) { }

                await _snapshotProcess.TerminateAsync();
            }
            catch (OperationCanceledException) { }
            catch (Exception exc)
            {
                _disposalSource.SetException(exc);
                return;
            }

            _disposalSource.SetResult(0);
        }

        public void Dispose()
        {
            lock (_lock)
            {
                if (_disposal == null)
                    _disposal = DisposeInternalAsync();
            }
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal;
        }

        #endregion

        private async Task SnapshotProcess(CancellationToken cancellation)
        {
            var interval = _options.SnapshotInterval;

            if (interval < 0)
                interval = 60 * 60 * 1000;

            while (cancellation.ThrowOrContinue())
            {
                try
                {
                    await SnapshotAsync(cancellation);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception)
                {
                    // TODO: Logging
                }

                await Task.Delay(_options.SnapshotInterval, cancellation);
            }
        }

        private async Task SnapshotAsync(CancellationToken cancellation)
        {
            var snapshotRevisionThreshold = _options.SnapshotRevisionThreshold;

            if (snapshotRevisionThreshold < 0)
                snapshotRevisionThreshold = 20;

            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                var entityStorageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();
                var enumerator = default(IAsyncEnumerator<IStream>);
                try
                {
                    enumerator = _streamStore.OpenStreamsToSnapshotAsync(snapshotRevisionThreshold, cancellation).GetEnumerator();

                    while (await enumerator.MoveNext(cancellation))
                    {
                        var stream = enumerator.Current;

                        if (stream.Snapshot == null && !stream.Commits.Any())
                            continue;

                        var serializedEntity = default(JToken);

                        if (stream.Snapshot == null)
                        {
                            serializedEntity = StreamRoot;
                        }
                        else
                        {
                            serializedEntity = JToken.Parse(CompressionHelper.Unzip(stream.Snapshot.Payload as byte[]));
                        }

                        foreach (var commit in stream.Commits)
                        {
                            serializedEntity = _differ.Patch(serializedEntity, JToken.Parse(CompressionHelper.Unzip(commit.Body as byte[])));
                        }

                        await stream.AddSnapshotAsync(CompressionHelper.Zip(serializedEntity.ToString()), cancellation);
                    }
                }
                finally
                {
                    enumerator?.Dispose();
                }
            }
        }
    }
}
