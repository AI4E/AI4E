using System;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    public interface IStoredSessionManager
    {
        IStoredSession AddEntry(IStoredSession storedSession, CoordinationEntryPath entryPath);
        IStoredSession Begin(SessionIdentifier session, DateTime leaseEnd);
        IStoredSession Copy(IStoredSession storedSession);
        IStoredSession End(IStoredSession storedSession);
        bool IsEnded(IStoredSession storedSession);
        IStoredSession RemoveEntry(IStoredSession storedSession, CoordinationEntryPath entryPath);
        IStoredSession UpdateLease(IStoredSession storedSession, DateTime leaseEnd);
    }
}
