using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;

namespace AI4E.Storage.Domain
{
    public sealed class CommitDispatcher : ICommitDispatcher
    {
        #region Fields

        private readonly IStreamPersistence _persistence;
        private readonly IMessageDispatcher _eventDispatcher;
        private readonly IProjectionEngine _projectionEngine;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommitDispatcher> _logger;
        private readonly DomainStorageOptions _options;
        private readonly AsyncInitializationHelper _initializationHelper;

        #endregion

        #region C'tor

        public CommitDispatcher(IStreamPersistence persistence,
                                IMessageDispatcher eventDispatcher,
                                IProjectionEngine projectionEngine,
                                IServiceProvider serviceProvider,
                                IOptions<DomainStorageOptions> optionsAccessor,
                                ILogger<CommitDispatcher> logger = null)
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            if (eventDispatcher == null)
                throw new ArgumentNullException(nameof(eventDispatcher));

            if (projectionEngine == null)
                throw new ArgumentNullException(nameof(projectionEngine));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            if (optionsAccessor == null)
                throw new ArgumentNullException(nameof(optionsAccessor));

            _persistence = persistence;
            _eventDispatcher = eventDispatcher;
            _projectionEngine = projectionEngine;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _options = optionsAccessor.Value ?? new DomainStorageOptions();

            _initializationHelper = new AsyncInitializationHelper(InitializeInternalAsync);
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            _logger?.LogDebug(Resources.GettingUndispatchedCommits);
            foreach (var commit in await _persistence.GetUndispatchedCommitsAsync(cancellation))
            {
                DispatchInternalAsync(commit, tcs: null, cancellation).HandleExceptions(_logger);
            }
        }

        #endregion

        public async Task DispatchAsync(ICommit commit, CancellationToken cancellation)
        {
            await _initializationHelper.Initialization;
            var tcs = new TaskCompletionSource<object>();
            DispatchInternalAsync(commit, tcs, cancellation).HandleExceptions(_logger);
            await tcs.Task;
        }

        private async Task DispatchInternalAsync(ICommit commit, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            var bucket = commit.BucketId;

            // The commit is not in our scope.
            // TODO: Remove the dependency on EntityStorageEngine. Add a type for bucket-id to type and type to bucket-id translation.
            if (!EntityStorageEngine.IsInScope(bucket, _options.Scope, out var typeName))
            {
                return;
            }

            var maxNumberOfCommitAttempts = int.MaxValue;

            for (var attempt = 1; attempt <= maxNumberOfCommitAttempts; attempt++)
            {
                try
                {
                    if (attempt == 1)
                    {
                        _logger?.LogDebug($"Dispatching commit '{commit.ConcurrencyToken}' of stream '{commit.StreamId}'.");
                    }
                    else
                    {
                        _logger?.LogDebug($"Dispatching commit '{commit.ConcurrencyToken}' of stream '{commit.StreamId}' ({attempt}. attempt).");
                    }

                    var projection = ProjectAsync(typeName, commit.StreamId, tcs, cancellation);
                    var dispatch = DispatchCoreAsync(commit, cancellation);

                    await Task.WhenAll(projection, dispatch);

                    var success = await dispatch;

                    if (success)
                    {
                        await _persistence.MarkCommitAsDispatchedAsync(commit, cancellation);
                        Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
                        return;
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Dispatching commit '{commit.ConcurrencyToken}' of stream '{commit.StreamId}' failed.");

                    Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();
                }

                // Calculate wait time in seconds
                var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

                await Task.Delay(timeToWait);
            }

            _logger?.LogError($"Dispatching commit '{commit.ConcurrencyToken}' of stream '{commit.StreamId}' finally failed.");
        }

        private async Task<bool> DispatchCoreAsync(ICommit commit, CancellationToken cancellation)
        {
            IEnumerable<object> events;

            using (var scope = _serviceProvider.CreateScope())
            {
                var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
                var settingsResolver = scope.ServiceProvider.GetRequiredService<ISerializerSettingsResolver>();
                var jsonSerializer = JsonSerializer.Create(settingsResolver.ResolveSettings(storageEngine));

                object Deserialize(byte[] data)
                {
                    if (data == null)
                        return null;

                    var str = CompressionHelper.Unzip(data);

                    using (var textReader = new StringReader(str))
                    {
                        return jsonSerializer.Deserialize(textReader, typeof(object));
                    }
                }

                // We need to evaluate the enumerable here, to ensure that the Deserialize method is not called outside the scope.
                events = commit.Events.Select(p => Deserialize(p.Body as byte[])).ToList();
            }

            var dispatchResults = await Task.WhenAll(events.Select(p => DispatchEventAsync(p, cancellation)));
            var dispatchResult = new AggregateDispatchResult(dispatchResults);

            if (!dispatchResult.IsSuccess)
            {
                _logger?.LogWarning($"Dispatching commit {commit.ConcurrencyToken} of stream {commit.StreamId} failed for reason: {dispatchResult.Message}.");
            }

            return dispatchResult.IsSuccess;
        }

        private Task<IDispatchResult> DispatchEventAsync(object evt, CancellationToken cancellation)
        {
            var dispatchData = DispatchDataDictionary.Create(evt.GetType(), evt);
            return _eventDispatcher.DispatchLocalAsync(dispatchData, publish: true, cancellation);
        }

        private async Task ProjectAsync(string bucketId, string id, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            try
            {
                var type = TypeLoadHelper.LoadTypeFromUnqualifiedName(bucketId);

                if (type == null)
                {
                    throw new InvalidOperationException($"Unable to load type for bucket '{bucketId}'");
                }

                await _projectionEngine.ProjectAsync(type, id, cancellation);
                Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
            }
            catch (Exception exc)
            {
                Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();

                throw;
            }
        }

        // Adapted from: https://stackoverflow.com/questions/383587/how-do-you-do-integer-exponentiation-in-c
        private static int Pow(int x, int pow)
        {
            if (pow < 0)
                throw new ArgumentOutOfRangeException(nameof(pow));

            var result = 1;
            while (pow != 0)
            {
                if ((pow & 1) == 1)
                    result *= x;
                x *= x;
                pow >>= 1;
            }

            if (result < 0)
                return int.MaxValue;

            return result;
        }
    }
}
