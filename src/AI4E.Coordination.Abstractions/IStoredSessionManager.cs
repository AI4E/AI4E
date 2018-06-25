using System;

namespace AI4E.Coordination
{
    public interface IStoredSessionManager
    {
        IStoredSession AddEntry(IStoredSession storedSession, string entry);
        IStoredSession Begin(string key, DateTime leaseEnd);
        IStoredSession Copy(IStoredSession storedSession);
        IStoredSession End(IStoredSession storedSession);
        bool IsEnded(IStoredSession storedSession);
        IStoredSession RemoveEntry(IStoredSession storedSession, string entry);
        IStoredSession UpdateLease(IStoredSession storedSession, DateTime leaseEnd);
    }
}