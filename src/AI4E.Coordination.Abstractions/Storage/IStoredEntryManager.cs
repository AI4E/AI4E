using System;
using AI4E.Coordination.Session;

namespace AI4E.Coordination.Storage
{
    public interface IStoredEntryManager
    {
        IStoredEntry AcquireReadLock(IStoredEntry storedEntry, CoordinationSession session);
        IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, CoordinationSession session);
        IStoredEntry AddChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, CoordinationSession session);
        IStoredEntry Copy(IStoredEntry storedEntry);
        IStoredEntry Create(CoordinationEntryPath path, CoordinationSession session, bool isEphemeral, ReadOnlySpan<byte> value);
        IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, CoordinationSession session);
        IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry, CoordinationSession session);
        IStoredEntry Remove(IStoredEntry storedEntry, CoordinationSession session);
        IStoredEntry RemoveChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child, CoordinationSession session);
        IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value, CoordinationSession session);
    }
}
