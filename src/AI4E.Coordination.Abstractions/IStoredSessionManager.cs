using System;

namespace AI4E.Coordination
{
    public interface IStoredSessionManager
    {
        IStoredSession AddEntry(IStoredSession storedSession, CoordinationEntryPath entryPath);
        IStoredSession Begin(string key, DateTime leaseEnd);
        IStoredSession Copy(IStoredSession storedSession);
        IStoredSession End(IStoredSession storedSession);
        bool IsEnded(IStoredSession storedSession);
        IStoredSession RemoveEntry(IStoredSession storedSession, CoordinationEntryPath entryPath);
        IStoredSession UpdateLease(IStoredSession storedSession, DateTime leaseEnd);
    }
}