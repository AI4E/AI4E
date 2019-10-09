using System.Diagnostics;

namespace AI4E.Coordination
{
    internal static class StoredEntryUtil
    {
        public static bool AreVersionEqual(IStoredEntry left, IStoredEntry right)
        {
            if (left is null)
                return right is null;

            if (right is null)
                return false;

            Debug.Assert(left.Path == right.Path);

            return left.StorageVersion == right.StorageVersion;
        }
    }
}
