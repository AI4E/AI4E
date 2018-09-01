using System;

namespace AI4E.Coordination
{
    public interface IStoredEntryManager
    {
        IStoredEntry AcquireReadLock(IStoredEntry storedEntry, Session session);
        IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, Session session);
        IStoredEntry AddChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, Session session);
        IStoredEntry Copy(IStoredEntry storedEntry);
        IStoredEntry Create(CoordinationEntryPath path, Session session, bool isEphemeral, ReadOnlySpan<byte> value);
        IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, Session session);
        IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry, Session session);
        IStoredEntry Remove(IStoredEntry storedEntry, Session session);
        IStoredEntry RemoveChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, Session session);
        IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value, Session session);
    }
}