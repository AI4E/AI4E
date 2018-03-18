using System;
using System.Threading;
using System.Threading.Tasks;
using Nito.AsyncEx;
using static System.Diagnostics.Debug;

namespace AI4E.Async
{
    public struct AsyncDisposeHelper : IAsyncDisposable
    {
        private Task _disposal;
        private readonly TaskCompletionSource<byte> _disposalSource;
        private readonly Func<Task> _asyncDisposal;
        private readonly AsyncReaderWriterLock _lock;

        public AsyncDisposeHelper(Func<Task> asyncDisposal)
        {
            if (asyncDisposal == null)
                throw new ArgumentNullException(nameof(asyncDisposal));

            _asyncDisposal = asyncDisposal;

            _disposalSource = new TaskCompletionSource<byte>();
            _lock = new AsyncReaderWriterLock();
            _disposal = null;
        }

        public void Dispose()
        {
            if (_disposalSource == null)
                return;

            Assert(_asyncDisposal != null);

            lock (_disposalSource)
            {
                if (_disposal == null)
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

        public AwaitableDisposable<IDisposable> ProhibitDisposalAsync(CancellationToken cancellation)
        {
            return _lock.ReaderLockAsync(cancellation);
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
