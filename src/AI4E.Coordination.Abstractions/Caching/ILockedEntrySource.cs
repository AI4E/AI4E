using System;
using System.Threading;
using System.Threading.Tasks;

namespace AI4E.Coordination.Caching
{
    public interface ILockedEntrySource
    {
        int Token { get; }
        void Unlock(int token);
        ValueTask GetUnlockTask(int token);
        ReadOnlyMemory<byte> GetValue(int token);
        bool IsExisting(int token);

        void CreateOrUpdate(int token, ReadOnlyMemory<byte> value);
        void Delete(int token);

#if !SUPPORTS_TRANSACTIONS

        // This is a temporary addition that is needed for the coordination service to consistently create entries.
        // This will be removed when we have ACID support for multi-entry changes.
        ValueTask FlushAsync(int token, CancellationToken cancellation);

#endif
    }
}
