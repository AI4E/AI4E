using System;
using System.Linq;
using System.Text;
using static System.Diagnostics.Debug;

namespace AI4E.Internal
{
    internal static class EntryPathHelper
    {
        private const char _seperatorChar = '/';
        private const string _seperatorString = "/";
        private const string _sessionSeperatorString = "->";
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
            if (path.Length == 1 && lastIndexOfSeparator == 0)
            {
                childName = string.Empty;
                return null;
            }

            // Separator is not last char
            Assert(lastIndexOfSeparator < normalizedPath.Length - 1);

            childName = normalizedPath.Substring(lastIndexOfSeparator + 1);

            // The parent node is the root node
            if (lastIndexOfSeparator == 0)
            {
                return "/";
            }

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

        // Gets the entry name that is roughly {path}->{session}
        public static string GetEntryName(string path, string session)
        {
            var resultsBuilder = new StringBuilder(path.Length +
                                                   session.Length +
                                                   EscapeHelper.CountCharsToEscape(path) +
                                                   EscapeHelper.CountCharsToEscape(session) +
                                                   1);
            resultsBuilder.Append(path);

            EscapeHelper.Escape(resultsBuilder, 0);

            var sepIndex = resultsBuilder.Length;

            resultsBuilder.Append(' ');
            resultsBuilder.Append(' ');

            resultsBuilder.Append(session);

            EscapeHelper.Escape(resultsBuilder, sepIndex + 2);

            // We need to ensure that the created entry is unique.
            resultsBuilder[sepIndex] = _sessionSeperatorString[0];
            resultsBuilder[sepIndex + 1] = _sessionSeperatorString[1];

            return resultsBuilder.ToString();
        }

        // TODO: Rename
        public static string ExtractRoute(string path)
        {
            var nameIndex = path.LastIndexOfAny(_pathSeperators);
            var index = path.IndexOf(_sessionSeperatorString);

            if (index == -1)
            {
                // TODO: Log warning
                return null;
            }

            var resultBuilder = new StringBuilder(path, startIndex: nameIndex + 1, length: index - nameIndex - 1, capacity: index);

            EscapeHelper.Unescape(resultBuilder, startIndex: 0);

            return resultBuilder.ToString();
        }

        // TODO: Rename
        public static string ExtractSession(string path)
        {
            var index = path.IndexOf(_sessionSeperatorString);

            if (index == -1)
            {
                // TODO: Log warning
                return null;
            }

            var resultBuilder = new StringBuilder(path, startIndex: index+2, length: path.Length - index - 2, capacity: path.Length - index - 2);

            EscapeHelper.Unescape(resultBuilder, startIndex: 0);

            return resultBuilder.ToString();
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
