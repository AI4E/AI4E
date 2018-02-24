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

namespace AI4E.Modularity
{
    [TypeConverter(typeof(ModuleVersionFilterConverter))]
    public struct ModuleVersionFilter : IEquatable<ModuleVersionFilter>
    {
        public static ModuleVersionFilter None { get; } = new ModuleVersionFilter();

        private readonly int? _majorVersion;
        private readonly int? _minorVersion;
        private readonly int? _revision;

        public ModuleVersionFilter(int? majorVersion, int? minorVersion, int? revision)
        {
            if (majorVersion != null && majorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(majorVersion));

            if (minorVersion != null && minorVersion < 0)
                throw new ArgumentOutOfRangeException(nameof(minorVersion));

            if (revision != null && revision < 0)
                throw new ArgumentOutOfRangeException(nameof(revision));

            _majorVersion = majorVersion;
            _minorVersion = minorVersion;
            _revision = revision;
        }

        public int? MajorVersion => _majorVersion;
        public int? MinorVersion => _minorVersion;
        public int? Revision => _revision;

        public bool IsFullyQualified => _majorVersion != null && _minorVersion != null && _revision != null;

        public bool Equals(ModuleVersionFilter other)
        {
            return MajorVersion == other.MajorVersion &&
                   MinorVersion == other.MinorVersion &&
                   Revision == other.Revision;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleVersionFilter other && Equals(other);
        }

        public override int GetHashCode()
        {
            return MajorVersion.GetHashCode() ^
                   MinorVersion.GetHashCode() ^
                   Revision.GetHashCode();
        }

        public override string ToString()
        {
            return $"{GetComponentString(MajorVersion)}.{GetComponentString(MinorVersion)}.{GetComponentString(Revision)}";
        }

        public static bool operator ==(ModuleVersionFilter left, ModuleVersionFilter right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(ModuleVersionFilter left, ModuleVersionFilter right)
        {
            return !left.Equals(right);
        }

        private static string GetComponentString(int? component)
        {
            if (component == null)
                return "*";

            return component.ToString();
        }

        public static ModuleVersionFilter Parse(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                throw new ArgumentNullOrWhiteSpaceException(nameof(value));

            var components = value.Split('.');

            if (components.Length == 0 || components.Length > 3)
                throw new FormatException();

            var c0 = components[0].Trim();

            int? major = c0 == "*" ? null : (int?)int.Parse(c0);
            int? minor = null;
            int? revision = null;

            if (components.Length > 1)
            {
                var c1 = components[1].Trim();

                minor = c1 == "*" ? null : (int?)int.Parse(c1);
            }

            if (components.Length > 2)
            {
                var c2 = components[2].Trim();

                revision = c2 == "*" ? null : (int?)int.Parse(c2);
            }

            return new ModuleVersionFilter(major, minor, revision);
        }

        public bool PassesFilter(ModuleVersion moduleVersion)
        {
            // TODO
            throw new NotImplementedException();
        }
    }

    public sealed class ModuleVersionFilterConverter : TypeConverter
    {
        public override bool CanConvertFrom(ITypeDescriptorContext context, Type sourceType)
        {
            return sourceType == typeof(ModuleVersionFilter) || sourceType == typeof(ModuleVersionFilter?) || sourceType == typeof(string) || base.CanConvertFrom(context, sourceType);
        }

        public override bool CanConvertTo(ITypeDescriptorContext context, Type destinationType)
        {
            return destinationType == typeof(ModuleVersionFilter) || destinationType == typeof(ModuleVersionFilter?) || destinationType == typeof(string) || base.CanConvertTo(context, destinationType);
        }

        public override object ConvertFrom(ITypeDescriptorContext context, CultureInfo culture, object value)
        {
            switch (value)
            {
                case ModuleVersionFilter moduleVersion:
                    return moduleVersion;

                case string str:
                    return ModuleVersionFilter.Parse(str);

                default:
                    return base.ConvertFrom(context, culture, value);
            }
        }

        public override object ConvertTo(ITypeDescriptorContext context, CultureInfo culture, object value, Type destinationType)
        {
            if (value is ModuleVersionFilter moduleVersion)
            {
                if (destinationType == typeof(ModuleVersionFilter) || destinationType == typeof(ModuleVersionFilter?))
                {
                    return moduleVersion;
                }

                if (destinationType == typeof(string))
                {
                    return moduleVersion.ToString();
                }
            }

            return base.ConvertTo(context, culture, value, destinationType);
        }
    }
}
