using System;

namespace AI4E.Coordination
{
    public interface IStoredEntryManager
    {
        IStoredEntry AcquireReadLock(IStoredEntry storedEntry, string session);
        IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, string session);
        IStoredEntry AddChild(IStoredEntry storedEntry, string name);
        IStoredEntry Copy(IStoredEntry storedEntry);
        IStoredEntry Create(string key, string session, bool isEphemeral, ReadOnlySpan<byte> value);
        IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, string session);
        IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry);
        IStoredEntry Remove(IStoredEntry storedEntry);
        IStoredEntry RemoveChild(IStoredEntry storedEntry, string name);
        IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value);
    }
}