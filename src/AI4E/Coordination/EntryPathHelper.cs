using System;
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

            var segments = path.Split(_pathSeperators, StringSplitOptions.None);

            var normalizedPathBuilder = new StringBuilder();

            foreach (var segment in segments)
            {
                int segmentStart, segmentEnd;

                for (segmentStart = 0; segmentStart < segment.Length && char.IsWhiteSpace(segment[segmentStart]); segmentStart++) ;

                // Empty segment
                if (segmentStart == segment.Length)
                    continue;

                for (segmentEnd = segment.Length - 1; segmentEnd > segmentStart && char.IsWhiteSpace(segment[segmentEnd]); segmentEnd--) ;

                var length = segmentEnd - segmentStart + 1;

                // Empty segment
                if (length == 0)
                    continue;

                normalizedPathBuilder.Append(_seperatorChar);
                normalizedPathBuilder.Append(segment, segmentStart, length);
            }

            if (normalizedPathBuilder.Length == 0)
            {
                return _seperatorString;
            }

            return normalizedPathBuilder.ToString();
        }

        public static string GetParentPath(string path, out string name, bool normalize = true)
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
                name = string.Empty;
                return null;
            }

            // Separator is not last char
            Assert(lastIndexOfSeparator < normalizedPath.Length - 1);

            name = normalizedPath.Substring(lastIndexOfSeparator + 1);
            return normalizedPath.Substring(0, lastIndexOfSeparator);
        }

        public static string GetChildPath(string path, string childName, bool normalize = true)
        {
            if (path == null)
                throw new ArgumentNullException(nameof(path));

            if (childName == null)
                throw new ArgumentNullException(nameof(childName));

            var result = path + _seperatorString + childName;

            if (normalize)
            {
                return NormalizePath(result);
            }

            return result;
        }
    }
}
