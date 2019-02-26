using System;
using System.Threading;
using System.Threading.Tasks;
using AI4E.Utils.Async;

namespace AI4E.Coordination.Caching
{
    public readonly struct LockedEntry : IAsyncDisposable
    {
        private readonly ILockedEntrySource _source;
        private readonly int _token;

        public LockedEntry(string key, ILockedEntrySource source, LockType lockType)
        {
            Key = key;
            _source = source;
            LockType = lockType;
            _token = source.Token;
        }

        public string Key { get; }
        public LockType LockType { get; }

        public ReadOnlyMemory<byte> Value
        {
            get
            {
                if (_source == null)
                    return ReadOnlyMemory<byte>.Empty;

                return _source.GetValue(_token);
            }
        }

        public bool IsExisting
        {
            get
            {
                if (_source == null)
                    return false;

                return _source.IsExisting(_token);
            }
        }

        public void CreateOrUpdate(ReadOnlyMemory<byte> value)
        {
            if (LockType != LockType.Exclusive)
                throw new NotSupportedException();

            _source?.CreateOrUpdate(_token, value);
        }

        public void Delete()
        {
            if (LockType != LockType.Exclusive)
                throw new NotSupportedException();

            _source?.Delete(_token);
        }

        public void Dispose()
        {
            _source?.Unlock(_token);
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Disposal.AsTask();
        }

        private ValueTask Disposal => _source?.GetUnlockTask(_token) ?? default;
        Task IAsyncDisposable.Disposal => Disposal.AsTask();

#if !SUPPORTS_TRANSACTIONS

        // This is a temporary addition that is needed for the coordination service to consistently create entries.
        // This may be removed when we have ACID support for multi-entry changes.

        /// <summary>
        /// Asynchronously flushes any changes to the underlying store without releasing the lock.
        /// </summary>
        /// <param name="cancellation">A <see cref="CancellationToken"/> used to cancel the asynchronous operation or <see cref="CancellationToken.None"/>.</param>
        /// <returns>A task representing the asynchronous operation.</returns>
        /// <remarks>
        /// This is not inteded to be called just before releasing the lock to flush the changes.
        /// When releasing the lock, all changes are flushed automatically.
        /// This is to flush changed WITHOUT releasing the lock.
        /// </remarks>
        public ValueTask FlushAsync(CancellationToken cancellation)
        {
            if (LockType != LockType.Exclusive)
                throw new NotSupportedException();

            return _source?.FlushAsync(_token, cancellation) ?? default;
        }

#endif
    }

    public enum LockType
    {
        Exclusive = 0,
        Shared = 1
    }
}
