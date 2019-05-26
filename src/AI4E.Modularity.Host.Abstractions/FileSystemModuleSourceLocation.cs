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
