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
using System.IO;

namespace AI4E.Modularity.Host
{
    public readonly struct FileSystemModuleSourceLocation : IEquatable<FileSystemModuleSourceLocation>
    {
        public FileSystemModuleSourceLocation(string location)
        {
            if (!IsValid(location, out var message))
            {
                throw new ArgumentException(message, nameof(location));
            }

            Location = location;
        }

        public string Location { get; }

        public bool Equals(FileSystemModuleSourceLocation other)
        {
            return Location == other.Location;
        }

        public override bool Equals(object obj)
        {
            return obj is FileSystemModuleSourceLocation location && Equals(location);
        }

        public override int GetHashCode()
        {
            return Location?.GetHashCode() ?? 0;
        }

        public static bool operator ==(in FileSystemModuleSourceLocation left, in FileSystemModuleSourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in FileSystemModuleSourceLocation left, in FileSystemModuleSourceLocation right)
        {
            return !left.Equals(right);
        }

        public static bool IsValid(string location, out string message)
        {
            if (string.IsNullOrWhiteSpace(location))
            {
                message = "The argument must neither be null, nor empty, nor whitespace only.";
                return false;
            }

            // TODO: Build a regec or the like for path checking
            // Adapted from: https://stackoverflow.com/questions/3137097/check-if-a-string-is-a-valid-windows-directory-folder-path/16526391#answer-48820213
            var isValid = true;

            try
            {
                var fullPath = Path.GetFullPath(location);
                isValid = Path.IsPathRooted(location);
            }
            catch
            {
                isValid = false;
            }

            if (!isValid)
            {
                message = "The argument is not a valid file path.";
            }
            else
            {
                message = default;
            }

            return isValid;
        }

        public static explicit operator FileSystemModuleSourceLocation(string location)
        {
            return new FileSystemModuleSourceLocation(location);
        }

        public static implicit operator string(FileSystemModuleSourceLocation location)
        {
            return location.Location;
        }
    }
}
