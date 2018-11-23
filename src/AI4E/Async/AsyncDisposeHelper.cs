using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Async
{
    public sealed class AsyncDisposeHelper : IAsyncDisposable
    {
        private Task _disposal;
        private volatile bool _disposalStarted = false;

        private readonly TaskCompletionSource<byte> _disposalSource;
        private readonly Func<Task> _asyncDisposal;
        private readonly AsyncReaderWriterLock _lock;
        private readonly CancellationTokenSource _cts;

        public AsyncDisposeHelper(Func<Task> asyncDisposal)
        {
            if (asyncDisposal == null)
                throw new ArgumentNullException(nameof(asyncDisposal));

            _asyncDisposal = asyncDisposal;

            _disposalSource = new TaskCompletionSource<byte>();
            _lock = new AsyncReaderWriterLock();
            _disposal = null;
            _cts = new CancellationTokenSource();
        }

        public void Dispose()
        {
            // Volatile read op.
            if (_disposalStarted)
                return;

            lock (_disposalSource)
            {
                // We use a dedicated flag for specifying whether the operation was already started 
                // instead of simply check _executeTask for beeing set already to allow 
                // recursive calls to Execute() in the executed operation.
                if (_disposalStarted)
                    return;

                _disposalStarted = true;

                // The cancellation has to be done before locking, because this cancellation signals
                // holders of the prohibit disposal locks to cancel and allow disposal.
                _cts.Cancel();

                Assert(_disposal == null);

                _disposal = DisposeInternalAsync();
            }
        }

        private async Task DisposeInternalAsync()
        {
            using (await _lock.WriterLockAsync())
            {
                try
                {
                    await _asyncDisposal();
                }
                catch (OperationCanceledException) { }
                catch (Exception exc)
                {
                    _disposalSource.SetException(exc);
                    return;
                }

                _disposalSource.SetResult(0);
            }
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal;
        }

        public Task Disposal => _disposalSource?.Task ?? Task.CompletedTask;

        public bool IsDisposed
        {
            get
            {
                if (_disposalSource == null)
                    return false;

                lock (_disposalSource)
                {
                    return _disposal != null;
                }
            }
        }

        /// <summary>
        /// Gets a cancellation token that is canceled when disposal is requested.
        /// </summary>
        public CancellationToken DisposalRequested => _cts?.Token ?? default;

        public CancellationToken CancelledOrDisposed(CancellationToken cancellation)
        {
            if (_cts == null)
            {
                return cancellation;
            }

            if (cancellation == default)
            {
                return DisposalRequested;
            }

            var combinedCancellationTokenSource = CancellationTokenSource.CreateLinkedTokenSource(DisposalRequested, cancellation);

            return combinedCancellationTokenSource.Token;
        }

        public AwaitableDisposable<IDisposable> ProhibitDisposalAsync(CancellationToken cancellation)
        {
            return _lock.ReaderLockAsync(CancelledOrDisposed(cancellation));
        }

        public AwaitableDisposable<IDisposable> ProhibitDisposalAsync()
        {
            return _lock.ReaderLockAsync();
        }

        public IDisposable ProhibitDisposal()
        {
            return _lock.ReaderLock();
        }
    }
}
