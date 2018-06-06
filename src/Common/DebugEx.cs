using System.Diagnostics;

namespace AI4E.Internal
{
    public static class DebugEx
    {
        // condition is only checked if precondition mets.
        public static void Assert(bool precondition, bool condition)
        {
            // precondition => condition
            Debug.Assert(!precondition || condition);
        }
    }
}
