using System;
using System.Linq;
using System.Text;
using static System.Diagnostics.Debug;

namespace AI4E.Coordination
{
    internal static class EntryPathHelper
    {
        private const char _seperatorChar = '/';
        private const string _seperatorString = "/";
        private static readonly char[] _pathSeperators = { _seperatorChar, '\\' };

        public static string NormalizePath(string path)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var normalizedPathBuilder = new StringBuilder(path.Length);

            for (var segment = GetNextSegment(path, (0, 0)); segment.start < path.Length; segment = GetNextSegment(path, segment))
            {
                var length = segment.end - segment.start;

                // Empty segment
                if (length == 0)
                    continue;

                normalizedPathBuilder.Append(_seperatorChar);
                normalizedPathBuilder.Append(path, segment.start, length);
            }

            if (normalizedPathBuilder.Length == 0)
            {
                return _seperatorString;
            }

            return normalizedPathBuilder.ToString();
        }

        public static string GetParentPath(string path, out string childName, bool normalize = true)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            var normalizedPath = path;

            if (normalize)
            {
                normalizedPath = NormalizePath(path);
            }

            var lastIndexOfSeparator = normalizedPath.LastIndexOf(_seperatorChar);

            // This is the root node.
            if (lastIndexOfSeparator == 0)
            {
                childName = string.Empty;
                return null;
            }

            // Separator is not last char
            Assert(lastIndexOfSeparator < normalizedPath.Length - 1);

            childName = normalizedPath.Substring(lastIndexOfSeparator + 1);
            return normalizedPath.Substring(0, lastIndexOfSeparator);
        }

        public static string GetChildPath(string path, string childName, bool normalize = true)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (childName == null)
                throw new ArgumentNullException(nameof(childName));

            var resultsBuilder = new StringBuilder(path.Length + 1 + childName.Length);

            resultsBuilder.Append(path);
            resultsBuilder.Append(_seperatorChar);
            resultsBuilder.Append(childName);

            var result = resultsBuilder.ToString();

            if (normalize)
            {
                return NormalizePath(result);
            }

            return result;
        }

        // Inclusive segment start
        private static int GetSegmentStart(string path, int startIndex)
        {
            Assert(startIndex >= 0 && startIndex <= path.Length);

            var segmentStart = startIndex;
            for (; segmentStart < path.Length && IsWhitespaceOrSeperator(path, segmentStart); segmentStart++) ;

            return segmentStart;
        }

        private static bool IsWhitespaceOrSeperator(string path, int index)
        {
            return (_pathSeperators.Contains(path[index]) || char.IsWhiteSpace(path[index]));
        }

        // Exclusive segment end
        private static int GetSegmentEnd(string path, int segmentStart)
        {
            Assert(segmentStart >= 0 && segmentStart <= path.Length);

            var segmentEnd = segmentStart;
            for (; segmentEnd < path.Length && !IsWhitespaceOrSeperator(path, segmentEnd); segmentEnd++) ;

            return segmentEnd;
        }

        private static (int start, int end) GetNextSegment(string path, (int start, int end) previousSegment)
        {
            Assert(path != null);
            Assert(previousSegment.start >= 0 && previousSegment.start <= path.Length);
            Assert(previousSegment.end >= 0 && previousSegment.end <= path.Length);
            Assert(previousSegment.start <= previousSegment.end);

            var start = GetSegmentStart(path, previousSegment.end);
            var end = GetSegmentEnd(path, start);

            return (start, end);
        }
    }
}
