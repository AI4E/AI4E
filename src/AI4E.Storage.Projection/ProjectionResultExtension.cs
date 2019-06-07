using System.Diagnostics;

namespace AI4E.Storage.Projection
{
    internal static class ProjectionResultExtension
    {
        public static TTargetId GetId<TTargetId>(this IProjectionResult projectionResult)
        {
            Debug.Assert(projectionResult.ResultId != null);
            Debug.Assert(projectionResult.ResultId is TTargetId);

            return (TTargetId)projectionResult.ResultId;
        }
    }
}
