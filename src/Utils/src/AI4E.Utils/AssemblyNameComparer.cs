/* License
 * --------------------------------------------------------------------------------------------------------------------
 * This file is part of the AI4E distribution.
 *   (https://github.com/AI4E/AI4E)
 * Copyright (c) 2018 - 2019 Andreas Truetschel and contributors.
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
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace AI4E.Utils
{
    public sealed class AssemblyNameComparer : IEqualityComparer<AssemblyName>, IEqualityComparer
    {
        public static AssemblyNameComparer ByDisplayName { get; }
            = new AssemblyNameComparer((AssemblyNameComparisonOptions)(-1));
        public static AssemblyNameComparer BySimpleName { get; } = new AssemblyNameComparer(default);

        public AssemblyNameComparer(AssemblyNameComparisonOptions options)
        {
            Options = options;
        }

        public AssemblyNameComparisonOptions Options { get; }

        public new bool Equals(object? x, object? y)
        {
            // This also includes that x and y are both null.
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            if (x is AssemblyName assemblyNameX && y is AssemblyName assemblyNameY)
            {
                return EqualsCore(assemblyNameX, assemblyNameY);
            }

            return false; // TODO: Throw ArgumentException?
        }

        public bool Equals(AssemblyName? x, AssemblyName? y)
        {
            // This also includes that x and y are both null.
            if (ReferenceEquals(x, y))
                return true;

            if (x is null || y is null)
                return false;

            return EqualsCore(x, y);
        }

        private bool EqualsCore(AssemblyName x, AssemblyName y)
        {
            if (x.Name == null ^ y.Name == null)
            {
                return false;
            }

            if (!StringComparer.OrdinalIgnoreCase.Equals(x.Name, y.Name))
            {
                return false;
            }

            if ((Options & AssemblyNameComparisonOptions.Version) != 0)
            {
                if (x.Version == null ^ y.Version == null)
                {
                    return false;
                }

                if (x.Version != null && y.Version != null && x.Version != y.Version)
                {
                    return false;
                }
            }

            if ((Options & AssemblyNameComparisonOptions.Culture) != 0)
            {
                if (x.CultureInfo == null ^ y.CultureInfo == null)
                {
                    return false;
                }

                if (x.CultureInfo != null && y.CultureInfo != null && !x.CultureInfo.Equals(y.CultureInfo))
                {
                    return false;
                }
            }

            if ((Options & AssemblyNameComparisonOptions.PublicKeyToken) != 0)
            {
                var xPublicKeyToken = x.GetPublicKeyToken();
                var yPublicKeyToken = x.GetPublicKeyToken();

                if (xPublicKeyToken == null ^ yPublicKeyToken == null)
                {
                    return false;
                }

                if (xPublicKeyToken != null
                    && yPublicKeyToken != null
                    && !xPublicKeyToken.AsSpan().SequenceEqual(yPublicKeyToken.AsSpan()))
                {
                    return false;
                }
            }

            return true;
        }

        public int GetHashCode(object obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            return obj is AssemblyName assemblyName ? GetHashCodeCore(assemblyName) : 0;
        }

        public int GetHashCode(AssemblyName obj)
        {
            if (obj is null)
                throw new ArgumentNullException(nameof(obj));

            return GetHashCodeCore(obj);
        }

        private int GetHashCodeCore(AssemblyName assemblyName)
        {
            var hashCode = assemblyName.Name != null
                ? StringComparer.OrdinalIgnoreCase.GetHashCode(assemblyName.Name)
                : 0;

            if ((Options & AssemblyNameComparisonOptions.Version) != 0 && assemblyName.Version != null)
            {
                hashCode *= 23;
                hashCode += assemblyName.Version.GetHashCode();
            }

            if ((Options & AssemblyNameComparisonOptions.Culture) != 0 && assemblyName.CultureInfo != null)
            {
                hashCode *= 23;
                hashCode += assemblyName.CultureInfo.GetHashCode();
            }

            if ((Options & AssemblyNameComparisonOptions.PublicKeyToken) != 0)
            {
                var publicKeyToken = assemblyName.GetPublicKeyToken();

                if (publicKeyToken != null)
                {
                    hashCode *= 23;
                    hashCode += publicKeyToken.Length.GetHashCode() + 1;

                    if (publicKeyToken.Length > 0)
                    {
                        hashCode *= 23;
                        hashCode += BitConverter.ToUInt64(publicKeyToken, 0).GetHashCode();
                    }
                }
            }

            return hashCode;
        }
    }

    [Flags]
    public enum AssemblyNameComparisonOptions
    {
        Name = 0x00,
        Version = 0x01,
        Culture = 0x02,
        PublicKeyToken = 0x04
    }
}
