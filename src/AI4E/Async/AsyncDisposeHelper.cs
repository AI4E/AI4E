using System;
using System.Threading.Tasks;
using static System.Diagnostics.Debug;

namespace AI4E.Async
{
    public struct AsyncDisposeHelper : IAsyncDisposable
    {
        private Task _disposal;
        private readonly TaskCompletionSource<byte> _disposalSource;
        private readonly Func<Task> _asyncDisposal;

        public AsyncDisposeHelper(Func<Task> asyncDisposal)
        {
            _disposalSource = new TaskCompletionSource<byte>();
            _disposal = null;
            _asyncDisposal = asyncDisposal;
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

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal;
        }

        public Task Disposal => _disposalSource?.Task ?? Task.CompletedTask;
    }
}
