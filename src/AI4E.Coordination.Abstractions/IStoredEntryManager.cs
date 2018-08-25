using System;

namespace AI4E.Coordination
{
    public interface IStoredEntryManager
    {
        IStoredEntry AcquireReadLock(IStoredEntry storedEntry, string session);
        IStoredEntry AcquireWriteLock(IStoredEntry storedEntry, string session);
        IStoredEntry AddChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child);
        IStoredEntry Copy(IStoredEntry storedEntry);
        IStoredEntry Create(CoordinationEntryPath path, string session, bool isEphemeral, ReadOnlySpan<byte> value);
        IStoredEntry ReleaseReadLock(IStoredEntry storedEntry, string session);
        IStoredEntry ReleaseWriteLock(IStoredEntry storedEntry);
        IStoredEntry Remove(IStoredEntry storedEntry);
        IStoredEntry RemoveChild(IStoredEntry storedEntry, CoordinationEntryPathSegment child);
        IStoredEntry SetValue(IStoredEntry storedEntry, ReadOnlySpan<byte> value);
    }

    public static class StoredEntryManagerExtension
    {
        [Obsolete("Use AddChild(IStoredEntry, CoordinationEntryPathSegment)")]
        public static IStoredEntry AddChild(this IStoredEntryManager entryManager, IStoredEntry storedEntry, string name) 
        {
            // TODO: Is the name the escaped name?
            return entryManager.AddChild(storedEntry, CoordinationEntryPathSegment.FromEscapedSegment(name.AsMemory()));
        }

        [Obsolete("Use RemoveChild(IStoredEntry, CoordinationEntryPathSegment)")]
        public static IStoredEntry RemoveChild(this IStoredEntryManager entryManager, IStoredEntry storedEntry, string name)
        {
            // TODO: Is the name the escaped name?
            return entryManager.RemoveChild(storedEntry, CoordinationEntryPathSegment.FromEscapedSegment(name.AsMemory()));
        }

        [Obsolete("Use Create(CoordinationEntryPath, string, bool, ReadOnlySpan<byte>)")]
        public static IStoredEntry Create(this IStoredEntryManager entryManager, string key, string session, bool isEphemeral, ReadOnlySpan<byte> value)  
        {
            // TODO: Is the path the escaped path?
            return entryManager.Create(CoordinationEntryPath.FromEscapedPath(key.AsMemory()), session, isEphemeral, value);
        }
    }
}