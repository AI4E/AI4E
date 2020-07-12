using System.Diagnostics;

namespace AI4E.Storage.Coordination.Storage
{
    internal static class StoredEntryUtil
    {
        public static bool AreVersionEqual(IStoredEntry left, IStoredEntry right)
        {
            if (left is null)
                return right is null;

            if (right is null)
                return false;

            Debug.Assert(left.Key == right.Key);

            return left.StorageVersion == right.StorageVersion;
        }
    }
}
