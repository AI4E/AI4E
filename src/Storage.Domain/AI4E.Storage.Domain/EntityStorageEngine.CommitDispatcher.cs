using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Internal;
using AI4E.Messaging;
using AI4E.Storage.Projection;
using AI4E.Utils;
using AI4E.Utils.Async;
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
            _logger?.LogDebug("Getting undispatched commits from persistence engine.");
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

            var timeout = Task.Delay(500); // TODO: This should be configurable.
            await Task.WhenAny(tcs.Task, timeout);
        }

        private async Task DispatchInternalAsync(ICommit commit, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            var events = BuildEventsList(commit);

            if (!events.Any())
            {
                Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
                return;
            }

            var maxNumberOfCommitAttempts = int.MaxValue;

            for (var attempt = 1; attempt <= maxNumberOfCommitAttempts; attempt++)
            {
                try
                {
                    if (attempt == 1)
                    {
                        _logger?.LogDebug($"Dispatching commit '{commit.Headers[EntityStorageEngine.ConcurrencyTokenHeaderKey]}' of stream '{commit.StreamId}'.");
                    }
                    else
                    {
                        _logger?.LogDebug($"Dispatching commit '{commit.Headers[EntityStorageEngine.ConcurrencyTokenHeaderKey]}' of stream '{commit.StreamId}' ({attempt}. attempt).");
                    }

                    var success = await DispatchCoreAsync(events, cancellation);

                    if (success)
                    {
                        await _persistence.MarkCommitAsDispatchedAsync(commit, cancellation);
                        Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
                        return;
                    }
                    else
                    {
                        _logger?.LogWarning($"Dispatching commit {commit.Headers[EntityStorageEngine.ConcurrencyTokenHeaderKey]} of stream {commit.StreamId} failed.");
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    _logger?.LogWarning(exc, $"Dispatching commit '{commit.Headers[EntityStorageEngine.ConcurrencyTokenHeaderKey]}' of stream '{commit.StreamId}' failed.");

                    Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();
                }

                // Calculate wait time in seconds
                var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

                await Task.Delay(timeToWait);
            }

            _logger?.LogError($"Dispatching commit '{commit.Headers[EntityStorageEngine.ConcurrencyTokenHeaderKey]}' of stream '{commit.StreamId}' finally failed.");
        }

        private async Task<bool> DispatchCoreAsync(ImmutableList<object> events, CancellationToken cancellation)
        {
            var dispatchResults = await events.Select(p => DispatchEventAsync(p, cancellation)).WhenAll(preserveOrder: false);
            var dispatchResult = new AggregateDispatchResult(dispatchResults);
            return dispatchResult.IsSuccess;
        }

        private ImmutableList<object> BuildEventsList(ICommit commit)
        {
            // The commit is not in our scope.
            // TODO: Remove the dependency on EntityStorageEngine. Add a type for bucket-id to type and type to bucket-id translation.
            if (!EntityStorageEngine.IsInScope(commit.BucketId, _options.Scope, out var typeName))
            {
                return ImmutableList<object>.Empty; ;
            }

            var entityType = TypeResolver.Default.ResolveType(typeName.AsSpan());
            var entityId = commit.StreamId;

            var events = ImmutableList.CreateBuilder<object>();
            events.Add(new ProjectEntityMessage(entityType, entityId));

            using var scope = _serviceProvider.CreateScope();
            var storageEngine = scope.ServiceProvider.GetRequiredService<IEntityStorageEngine>();
            var settingsResolver = scope.ServiceProvider.GetRequiredService<ISerializerSettingsResolver>();
            var jsonSerializer = JsonSerializer.Create(settingsResolver.ResolveSettings(storageEngine));

            object Deserialize(byte[] data)
            {
                if (data == null)
                    return null;

                var str = CompressionHelper.Unzip(data);

                using var textReader = new StringReader(str);
                return jsonSerializer.Deserialize(textReader, typeof(object));
            }

            // We need to evaluate the enumerable here, to ensure that the Deserialize method is not called outside the scope.
            foreach (var eventMessage in commit.Events)
            {
                events.Add(Deserialize(eventMessage.Body as byte[]));
            }

            return events.ToImmutable();
        }

        private ValueTask<IDispatchResult> DispatchEventAsync(object evt, CancellationToken cancellation)
        {
            var dispatchData = DispatchDataDictionary.Create(evt.GetType(), evt);
            return _eventDispatcher.DispatchLocalAsync(dispatchData, publish: true, cancellation);
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
