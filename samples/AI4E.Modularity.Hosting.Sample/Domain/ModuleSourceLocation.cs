using System;
using static System.Diagnostics.Debug;

namespace AI4E.Modularity.Hosting.Sample.Domain
{
    public readonly struct ModuleSourceLocation : IEquatable<ModuleSourceLocation>
    {
        public ModuleSourceLocation(string location)
        {
            if (!TryGetUri(location, out var uri, out var message))
            {
                throw new ArgumentException(message, nameof(location));
            }

            Location = uri.AbsoluteUri;
            IsLocal = uri.IsFile;
        }

        public string Location { get; }

        public bool IsLocal { get; }

        public bool Equals(ModuleSourceLocation other)
        {
            return Location == other.Location;
        }

        public override bool Equals(object obj)
        {
            return obj is ModuleSourceLocation location && Equals(location);
        }

        public override int GetHashCode()
        {
            return Location?.GetHashCode() ?? 0;
        }

        public static bool operator ==(in ModuleSourceLocation left, in ModuleSourceLocation right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(in ModuleSourceLocation left, in ModuleSourceLocation right)
        {
            return !left.Equals(right);
        }

        private static bool TryGetUri(string location, out Uri result, out string message)
        {
            // TODO

            if (!Uri.TryCreate(location, UriKind.Absolute, out var uriResult))
            {
                message = "The argument is neither an local path nor a well-formed absolute uri.";
                result = default;
                return false;
            }

            Assert(uriResult.IsAbsoluteUri);

            if (uriResult.Scheme != Uri.UriSchemeHttp &&
               uriResult.Scheme != Uri.UriSchemeHttps &&
               uriResult.Scheme != Uri.UriSchemeFile)
            {
                message = "The specified location must be either a local path or a http(s) uri.";
                result = default;
                return false;
            }

            message = default;
            result = uriResult;
            return true;
        }

        public static bool IsValid(string location, out string message)
        {
            return TryGetUri(location, out _, out message);
        }

        public static explicit operator ModuleSourceLocation(string location)
        {
            return new ModuleSourceLocation(location);
        }
    }
}
