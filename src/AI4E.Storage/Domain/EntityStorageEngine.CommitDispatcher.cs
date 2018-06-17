using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Async;
using AI4E.DispatchResults;
using AI4E.Internal;
using AI4E.Processing;
using AI4E.Storage.Projection;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nito.AsyncEx;

namespace AI4E.Storage.Domain
{
    // TODO: Use AsyncInitializationHelper and AsyncDisposeHelper
    public sealed class CommitDispatcher : ICommitDispatcher, IAsyncDisposable
    {
        #region Fields

        private readonly IMessageDispatcher _eventDispatcher;
        private readonly IProjectionDependencyStore<string, string> _projectionDependencyStore;
        private readonly IProjector _projector;
        private readonly IDataStore _dataStore;
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<CommitDispatcher> _logger;

        private readonly CancellationTokenSource _cancellationSource;
        private readonly Task _initialization;
        private readonly IAsyncProcess _dispatchProcess;
        private readonly AsyncProducerConsumerQueue<(ICommit commit, int attempt, TaskCompletionSource<object> tcs)> _dispatchQueue;
        private readonly IStreamPersistence _persistence;

        #endregion

        #region C'tor

        public CommitDispatcher(IStreamPersistence persistence,
                                IMessageDispatcher eventDispatcher,
                                IProjectionDependencyStore<string, string> projectionDependencyStore,
                                IProjector projector,
                                IDataStore dataStore,
                                IServiceProvider serviceProvider,
                                ILogger<CommitDispatcher> logger = null)
        {
            if (persistence == null)
                throw new ArgumentNullException(nameof(persistence));

            if (eventDispatcher == null)
                throw new ArgumentNullException(nameof(eventDispatcher));

            if (projectionDependencyStore == null)
                throw new ArgumentNullException(nameof(projectionDependencyStore));

            if (projector == null)
                throw new ArgumentNullException(nameof(projector));

            if (dataStore == null)
                throw new ArgumentNullException(nameof(dataStore));

            if (serviceProvider == null)
                throw new ArgumentNullException(nameof(serviceProvider));

            _persistence = persistence;
            _eventDispatcher = eventDispatcher;
            _projectionDependencyStore = projectionDependencyStore;
            _projector = projector;
            _dataStore = dataStore;
            _serviceProvider = serviceProvider;
            _logger = logger;
            _dispatchQueue = new AsyncProducerConsumerQueue<(ICommit commit, int attempt, TaskCompletionSource<object> tcs)>();
            _cancellationSource = new CancellationTokenSource();
            _dispatchProcess = new AsyncProcess(DispatchProcess);
            _initialization = InitializeInternalAsync(_cancellationSource.Token);
        }

        #endregion

        #region Initialization

        private async Task InitializeInternalAsync(CancellationToken cancellation)
        {
            await _dispatchProcess.StartAsync();

            _logger?.LogDebug(Resources.GettingUndispatchedCommits);
            foreach (var commit in await _persistence.GetUndispatchedCommitsAsync(cancellation))
            {
                await _dispatchQueue.EnqueueAsync((commit, attempt: 1, tcs: null));
            }
        }

        #endregion

        #region Disposal

        private Task _disposal;
        private readonly TaskCompletionSource<byte> _disposalSource = new TaskCompletionSource<byte>();
        private readonly object _lock = new object();

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

                await _dispatchProcess.TerminateAsync();
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

        private async Task DispatchProcess(CancellationToken cancellation)
        {
            while (cancellation.ThrowOrContinue())
            {
                var (commit, attempt, tcs) = await _dispatchQueue.DequeueAsync(cancellation);

                try
                {
                    var projection = ProjectAsync(commit.BucketId, commit.StreamId, tcs, cancellation);
                    var dispatch = DispatchAsync(commit, cancellation);

                    await Task.WhenAll(projection, dispatch);

                    var success = await dispatch;

                    if (success)
                    {
                        await _persistence.MarkCommitAsDispatchedAsync(commit, cancellation);
                    }
                    else
                    {
                        Reschedule(commit, attempt, cancellation).HandleExceptions();
                    }
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested) { throw; }
                catch (Exception exc)
                {
                    // TODO: Log exception

                    Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();

                    Reschedule(commit, attempt, cancellation).HandleExceptions();
                }
            }
        }

        private async Task Reschedule(ICommit commit, int attempt, CancellationToken cancellation)
        {
            // Calculate wait time in seconds
            var timeToWait = TimeSpan.FromSeconds(Pow(2, attempt - 1));

            await Task.Delay(timeToWait);

            await _dispatchQueue.EnqueueAsync((commit, attempt + 1, tcs: null), cancellation);
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

        private async Task<bool> DispatchAsync(ICommit commit, CancellationToken cancellation)
        {
            var events = commit.Events.Select(p => p.Body);

            var dispatchResults = await Task.WhenAll(events.Select(p => DispatchSingleAsync(p, cancellation)));
            var dispatchResult = new AggregateDispatchResult(dispatchResults);

            // TODO: Log errors
            return dispatchResult.IsSuccess; // TODO: Throw an exception instead?
        }

        private Task<IDispatchResult> DispatchSingleAsync(object evt, CancellationToken cancellation)
        {
            return _eventDispatcher.DispatchAsync(evt, publish: true);
        }

        private async Task ProjectAsync(string bucketId, string id, TaskCompletionSource<object> tcs, CancellationToken cancellation)
        {
            var type = GetTypeFromBucket(bucketId);
            if (type == null)
            {
                throw new InvalidOperationException($"Unable to load type for bucket '{bucketId}'");
            }

            using (var scope = _serviceProvider.CreateScope())
            {
                var scopedServiceProvider = scope.ServiceProvider;
                var entityStorageEngine = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();

                async Task ProjectLocalAsync()
                {
                    try
                    {
                        await ProjectSingleAsync(type, id, entityStorageEngine, cancellation);
                        Task.Run(() => tcs?.TrySetResult(null)).HandleExceptions();
                    }
                    catch (Exception exc)
                    {
                        Task.Run(() => tcs?.TrySetException(exc)).HandleExceptions();
                        throw;
                    }
                }

                var tasks = new List<Task>
                    {
                        ProjectLocalAsync()
                    };

                var dependents = await _projectionDependencyStore.GetDependentsAsync(GetBucketId(type), id, cancellation);

                tasks.AddRange(dependents.Select(p => ProjectSingleAsync(GetTypeFromBucket(p.BucketId), p.Id, null, cancellation)));

                await Task.WhenAll(tasks);

                var oldDependencies = (await _projectionDependencyStore.GetDependenciesAsync(GetBucketId(type), id, cancellation)).Select(p => (bucket: p.BucketId, id: p.Id));
                var dependencies = entityStorageEngine.CachedEntries.Where(p => !(p.id.Equals(id) && p.revision == default && p.type == type)).Select(p => (bucket: GetBucketId(p.type), p.id));

                var added = dependencies.Except(oldDependencies);
                var removed = oldDependencies.Except(dependencies);

                foreach (var a in added)
                {
                    await _projectionDependencyStore.AddDependencyAsync(GetBucketId(type), id, a.bucket, a.id, cancellation);
                }

                foreach (var a in removed)
                {
                    await _projectionDependencyStore.RemoveDependencyAsync(GetBucketId(type), id, a.bucket, a.id, cancellation);
                }
            }
        }

        private async Task ProjectSingleAsync(Type type,
                                              string id,
                                              IEntityStorageEngine entityStorageEngine,
                                              CancellationToken cancellation)
        {
            var entity = default(object);

            if (entityStorageEngine == null)
            {
                using (var scope = _serviceProvider.CreateScope())
                {
                    var scopedServiceProvider = scope.ServiceProvider;
                    var entityStorageEngine2 = scopedServiceProvider.GetRequiredService<IEntityStorageEngine>();

                    entity = await entityStorageEngine2.GetByIdAsync(type, id, cancellation: default);

                }
            }
            else
            {
                entity = await entityStorageEngine.GetByIdAsync(type, id, cancellation: default);
            }

            var projectionResults = await _projector.ProjectAsync(entity.GetType(), entity, cancellation);

            foreach (var projectionResult in projectionResults)
            {
                await _dataStore.StoreAsync(projectionResult.ResultType, projectionResult.Result, cancellation);
            }
        }

        public Task DispatchAsync(ICommit commit)
        {
            var tcs = new TaskCompletionSource<object>();

            return Task.WhenAll(_dispatchQueue.EnqueueAsync((commit, attempt: 1, tcs)), tcs.Task);
        }

        #region Helpers

        private static string GetBucketId(Type entityType)
        {
            return entityType.ToString();
        }

        private static Type GetTypeFromBucket(string bucketId)
        {
            return TypeLoadHelper.LoadTypeFromUnqualifiedName(bucketId);
        }

        #endregion 
    }
}
