/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 Andreas Truetschel and contributors.
 * 
 * AI4E is free software: you can redistribute it and/or modify  
 * it under the terms of the GNU Lesser General Public License as   
 * published by the Free Software Foundation, version 3.
 *
 * AI4E is distributed in the hope that it will be useful, but 
 * WITHOUT ANY WARRANTY; without even the implied warranty of 
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE. See the GNU 
 * Lesser General Public License for more details.
 *
 * You should have received a copy of the GNU Lesser General Public License
 * along with this program. If not, see <http://www.gnu.org/licenses/>.
 * --------------------------------------------------------------------------------------------------------------------
 */

using System;
using System.ComponentModel;
using System.Globalization;
using System.Text.RegularExpressions;

namespace AI4E.Modularity.Metadata
{
    [TypeConverter(typeof(ModuleVersionRangeTypeConverter))]
    public readonly struct ModuleVersionRange : IEquatable<ModuleVersionRange>
    {
        public static ModuleVersionRange NoPreReleases { get; } = new ModuleVersionRange();
        public static ModuleVersionRange All { get; } = new ModuleVersionRange(majorVersion: null, minorVersion: null, revision: null, allowPreReleases: true);

        // TODO: Rename to Exact
        public static ModuleVersionRange SingleVersion(ModuleVersion version)
        {
            return new ModuleVersionRange(version.MajorVersion,
                                          version.MinorVersion,
                                          version.Revision,
                                          version.MajorVersion,
                                          version.MinorVersion,
                                          version.Revision,
                                          version.IsPreRelease);
        }

        public static ModuleVersionRange Minimum(ModuleVersion version)
        {
            return new ModuleVersionRange(version.MajorVersion,
                                          version.MinorVersion,
                                          version.Revision,
                                          version.IsPreRelease);
        }

        #region C'tor

        public ModuleVersionRange(int? majorVersion, int? minorVersion, int? revision)
        {
            if (majorVersion != null && majorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(majorVersion));

            if (minorVersion != null && minorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(minorVersion));

            if (revision != null && revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            MinMajor = majorVersion;
            MinMinor = minorVersion;
            MinRevision = revision;

            MaxMajor = null;
            MaxMinor = null;
            MaxRevision = null;

            AllowPreReleases = false;
        }

        public ModuleVersionRange(int? majorVersion, int? minorVersion, int? revision, bool allowPreReleases)
        {
            if (majorVersion != null && majorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(majorVersion));

            if (minorVersion != null && minorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(minorVersion));

            if (revision != null && revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            MinMajor = majorVersion;
            MinMinor = majorVersion == null ? null : minorVersion;
            MinRevision = majorVersion == null || minorVersion == null ? null : revision;

            MaxMajor = null;
            MaxMinor = null;
            MaxRevision = null;

            AllowPreReleases = allowPreReleases;
        }

        public ModuleVersionRange(int? minMajorVersion,
                                  int? minMinorVersion,
                                  int? minRevision,
                                  int? maxMajorVersion,
                                  int? maxMinorVersion,
                                  int? maxRevision,
                                  bool allowPreReleases)
        {
            if (minMajorVersion != null && minMajorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(minMajorVersion));

            if (minMinorVersion != null && minMinorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(minMinorVersion));

            if (minRevision != null && minRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(minRevision));

            if (maxMajorVersion != null && maxMajorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMajorVersion));

            if (maxMinorVersion != null && maxMinorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(maxMinorVersion));

            if (maxRevision != null && maxRevision < 0)
                throw new ArgumentOutOfRangeException(nameof(maxRevision));

            if (minMajorVersion > maxMajorVersion ||
                minMajorVersion == maxMajorVersion && minMinorVersion > maxMinorVersion ||
                minMajorVersion == maxMajorVersion && minMinorVersion == maxMinorVersion && minRevision > maxRevision)
            {
                throw new ArgumentException("The major version must not be smaller than the minor version.");
            }

            MinMajor = minMajorVersion;
            MinMinor = minMajorVersion == null ? null : minMinorVersion;
            MinRevision = minMajorVersion == null || minMinorVersion == null ? null : minRevision;

            MaxMajor = maxMajorVersion;
            MaxMinor = maxMajorVersion == null ? null : maxMinorVersion;
            MaxRevision = maxMajorVersion == null || maxMinorVersion == null ? null : maxRevision;

            AllowPreReleases = allowPreReleases;
        }

        #endregion

        public int? MinMajor { get; }
        public int? MinMinor { get; }
        public int? MinRevision { get; }

        public int? MaxMajor { get; }
        public int? MaxMinor { get; }
        public int? MaxRevision { get; }

        public bool AllowPreReleases { get; }

        public bool Equals(in ModuleVersionRange other)
        {
            return MinMajor == other.MinMajor &&
                   MinMinor == other.MinMinor &&
                   MinRevision == other.MinRevision &&
                   MaxMajor == other.MaxMajor &&
                   MaxMinor == other.MaxMinor &&
                   MaxRevision == other.MaxRevision &&
                   AllowPreReleases == other.AllowPreReleases;
        }

        public bool Equals(ModuleVersionRange other)
        {
            return Equals(in other);
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleVersionRange versionRange && Equals(in versionRange);
        }

        public override int GetHashCode()
        {
            return MinMajor?.GetHashCode() ?? 0 ^
                   MinMinor?.GetHashCode() ?? 0 ^
                   MinRevision?.GetHashCode() ?? 0 ^
                   MaxMajor?.GetHashCode() ?? 0 ^
                   MaxMinor?.GetHashCode() ?? 0 ^
                   MaxRevision?.GetHashCode() ?? 0 ^
                   AllowPreReleases.GetHashCode();
        }

        public override string ToString()
        {
            if (MaxMajor == null && MaxMinor == null && MaxRevision == null)
            {
                if (!AllowPreReleases)
                {
                    return $"{GetStringFromComponent(MinMajor)}.{GetStringFromComponent(MinMinor)}.{GetStringFromComponent(MinRevision)}";
                }

                return $"{GetStringFromComponent(MinMajor)}.{GetStringFromComponent(MinMinor)}.{GetStringFromComponent(MinRevision)}-pre";
            }

            if (!AllowPreReleases)
            {
                return $"[{GetStringFromComponent(MinMajor)}.{GetStringFromComponent(MinMinor)}.{GetStringFromComponent(MinRevision)} {GetStringFromComponent(MaxMajor)}.{GetStringFromComponent(MaxMinor)}.{GetStringFromComponent(MaxRevision)}]";
            }

            return $"[{GetStringFromComponent(MinMajor)}.{GetStringFromComponent(MinMinor)}.{GetStringFromComponent(MinRevision)}-pre {GetStringFromComponent(MaxMajor)}.{GetStringFromComponent(MaxMinor)}.{GetStringFromComponent(MaxRevision)}]";
        }

        public static bool operator ==(in ModuleVersionRange left, in ModuleVersionRange right)
        {
            return left.Equals(in right);
        }

        public static bool operator !=(in ModuleVersionRange left, in ModuleVersionRange right)
        {
            return !left.Equals(in right);
        }

        public bool IsMatch(in ModuleVersion moduleVersion)
        {
            return (MinMajor == null || MinMajor <= moduleVersion.MajorVersion) &&
                   (MaxMajor == null || MaxMajor >= moduleVersion.MajorVersion) &&
                   (MinMinor == null || MinMinor <= moduleVersion.MinorVersion) &&
                   (MaxMinor == null || MaxMinor >= moduleVersion.MinorVersion) &&
                   (MinRevision == null || MinRevision <= moduleVersion.Revision) &&
                   (MaxRevision == null || MaxRevision >= moduleVersion.Revision) &&
                   (AllowPreReleases || !moduleVersion.IsPreRelease);
        }

        public bool TryCombine(in ModuleVersionRange other, out ModuleVersionRange result)
        {
            var minMajor = GetMax(MinMajor, other.MinMajor);
            var maxMajor = GetMin(MaxMajor, other.MaxMajor);

            if (minMajor > maxMajor)
            {
                result = default;
                return false;
            }

            var minMinor = GetMax(MinMinor, other.MinMinor);
            var maxMinor = GetMin(MaxMinor, other.MaxMinor);

            if (minMajor == maxMajor &&
                minMinor > maxMinor)
            {
                result = default;
                return false;
            }

            var minRevision = GetMax(MinRevision, other.MinRevision);
            var maxRevision = GetMin(MaxRevision, other.MaxRevision);

            if (minMajor == maxMajor &&
                minMinor == maxMinor &&
                minRevision > maxRevision)
            {
                result = default;
                return false;
            }

            var allowPreReleases = AllowPreReleases && other.AllowPreReleases;

            result = new ModuleVersionRange(minMajor, minMinor, minRevision, maxMajor, maxMinor, maxRevision, allowPreReleases);
            return true;
        }

        private int? GetMax(int? a, int? b)
        {
            if (a == null && b == null)
                return null;

            if (a != null)
                return b;

            if (b != null)
                return a;

            return Math.Max((int)a, (int)b);
        }

        private int? GetMin(int? a, int? b)
        {
            if (a == null && b == null)
                return null;

            if (a != null)
                return b;

            if (b != null)
                return a;

            return Math.Min((int)a, (int)b);
        }

        private static readonly string _regexPattern = $@"^\s*\[?\s*(?<{nameof(MinMajor)}>\*|\d+)\s*(?:\.\s*(?<{nameof(MinMinor)}>\*|\d+)\s*(?:\.\s*(?<{nameof(MinRevision)}>\*|\d+)\s*)?)?(?<{nameof(AllowPreReleases)}>-\s*pre\s*)?(?:\s+(?<{nameof(MaxMajor)}>\*|\d+)\s*(?:\.\s*(?<{nameof(MaxMinor)}>\*|\d+)\s*(?:\.\s*(?<{nameof(MaxRevision)}>\*|\d+)\s*)?)?)?\s*\]?$";
        private static readonly Regex _regex = new Regex(_regexPattern, RegexOptions.Compiled | RegexOptions.IgnoreCase | RegexOptions.Singleline);

        public static ModuleVersionRange Parse(string value)
        {
            if (value == null)
                throw new ArgumentNullException(nameof(value));

            var match = _regex.Match(value);

            if (!match.Success)
            {
                throw new FormatException();
            }

            var groups = match.Groups;

            var minMajorGroup = groups[nameof(MinMajor)];
            var minMajor = GetComponentFromString(minMajorGroup.Value);

            var minMinorGroup = groups[nameof(MinMinor)];
            var minMinor = minMinorGroup.Success ? GetComponentFromString(minMinorGroup.Value) : null;

            var minRevisionGroup = groups[nameof(MinRevision)];
            var minRevision = minRevisionGroup.Success ? GetComponentFromString(minRevisionGroup.Value) : null;

            var maxMajorGroup = groups[nameof(MaxMajor)];
            var maxMajor = maxMajorGroup.Success ? GetComponentFromString(maxMajorGroup.Value) : null;

            var maxMinorGroup = groups[nameof(MaxMinor)];
            var maxMinor = maxMinorGroup.Success ? GetComponentFromString(maxMinorGroup.Value) : null;

            var maxRevisionGroup = groups[nameof(MaxRevision)];
            var maxRevision = maxRevisionGroup.Success ? GetComponentFromString(maxRevisionGroup.Value) : null;

            var allowPreReleasesGroup = groups[nameof(AllowPreReleases)];
            var allowPreReleases = allowPreReleasesGroup.Success;

            return new ModuleVersionRange(minMajor, minMinor, minRevision, maxMajor, maxMinor, maxRevision, allowPreReleases);
        }

        private static string GetStringFromComponent(int? component)
        {
            if (component == null)
                return "*";

            return component.ToString();
        }

        private static int? GetComponentFromString(string value)
        {
            if (string.Equals(value, "*", StringComparison.OrdinalIgnoreCase))
                return null;

            return int.Parse(value);
        }
    }

    public sealed class ModuleVersionRangeTypeConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(ModuleVersionRange) || sourceType == typeof(ModuleVersionRange?) || sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(ModuleVersionRange) || destinationType == typeof(ModuleVersionRange?) || destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            switch (value)
            {
                case ModuleVersionRange moduleVersionRange:
                    return moduleVersionRange;

                case string str:
                    return ModuleVersionRange.Parse(str);

                default:
                    return base.ConvertFrom(context, culture, value);
            }
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is ModuleVersionRange moduleVersionRange)
            {
                if (destinationType == typeof(ModuleVersionRange) || destinationType == typeof(ModuleVersionRange?))
                {
                    return moduleVersionRange;
                }

                if (destinationType == typeof(string))
                {
                    return moduleVersionRange.ToString();
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
