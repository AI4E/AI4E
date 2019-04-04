using System;

namespace BookStore.Alerts
{
    /// <summary>
    /// Represents an alert message.
    /// </summary>
    public readonly struct AlertMessage
    {
        /// <summary>
        /// Creates a new instance of the <see cref="AlertMessage"/> type.
        /// </summary>
        /// <param name="alertType">A value of <see cref="AlertMessage"/> indicating the type of alert.</param>
        /// <param name="message">A <see cref="string"/> specifying the alert message.</param>
        /// <param name="expiration">
        /// The <see cref="DateTime"/> indicating the alert's expiration or <c>null</c> if the alert has no expiration.
        /// </param>
        /// <param name="allowDismiss">A boolean value indicating whether the alert may be dismissed.</param>
        /// <param name="uriFilter">
        /// An <see cref="UriFilter"/> that represents an uri filter that specifies on which
        /// pages the alert shall be displayed.
        /// </param>
        public AlertMessage(
            AlertType alertType,
            string message,
            DateTime? expiration = null,
            bool allowDismiss = false,
            UriFilter uriFilter = default)
        {
            AlertType = alertType;
            Message = message;
            Expiration = expiration;
            AllowDismiss = allowDismiss;
            UriFilter = uriFilter;
        }

        /// <summary>
        /// Gets the type of alert.
        /// </summary>
        public AlertType AlertType { get; }

        /// <summary>
        /// Gets the alert message.
        /// </summary>
        public string Message { get; }

        /// <summary>
        /// Gets the date and time of the alert's expiration or <c>null</c> if the alert has no expiration.
        /// </summary>
        public DateTime? Expiration { get; }

        /// <summary>
        /// Gets a boolean value indicating whether the alert may be dismissed.
        /// </summary>
        public bool AllowDismiss { get; }

        /// <summary>
        /// Gets an url filter that specifies on which pages the alert shall be displayed or <c>null</c> if it shall be displayed on all pages.
        /// </summary>
        public UriFilter UriFilter { get; }
    }

    /// <summary>
    /// A enumeration of alert types.
    /// </summary>
    public enum AlertType
    {
        /// <summary>
        /// Indicates no alert. The alert will never get displayed.
        /// </summary>
        None,

        /// <summary>
        /// Indicates an informational alert.
        /// </summary>
        Info,

        /// <summary>
        /// Indicates an alert that an action was successful.
        /// </summary>
        Success,

        /// <summary>
        /// Indicates a warning alert.
        /// </summary>
        Warning,

        /// <summary>
        /// Indicates a danger alert.
        /// </summary>
        Danger
    }

    /// <summary>
    /// Represents an uri filter.
    /// </summary>
    public readonly struct UriFilter : IEquatable<UriFilter>
    {
        private readonly string _uri;
        private readonly bool _exactMatch;

        public static UriFilter MatchAll => default;

        /// <summary>
        /// Creates a new instance of the <see cref="UriFilter"/> type.
        /// </summary>
        /// <param name="uri">The uri that represets the filter.</param>
        /// <param name="exactMatch">A boolean value indicating whether the uris must match exactly.</param>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        public UriFilter(string uri, bool exactMatch = false)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            _uri = uri.Trim();

            if (!_uri.StartsWith("/"))
            {
                _uri = "/" + _uri;
            }

            _exactMatch = exactMatch;
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified uri filter matches the current one.
        /// </summary>
        /// <param name="other">The <see cref="UriFilter"/> to compare with.</param>
        /// <returns>True if <paramref name="other"/> matches the current uri filter, false otherwise.</returns>
        public bool Equals(UriFilter other)
        {
            return _exactMatch == other._exactMatch &&
                   _uri == other._uri;
        }

        /// <summary>
        /// return a boolean value inidcating whether the specified object matched the current uri filter.
        /// </summary>
        /// <param name="obj">The <see cref="object"/> to compare with.</param>
        /// <returns>True if <paramref name="obj"/> is of type <see cref="UriFilter"/> and equals the current uri filter, false otherwise.</returns>
        public override bool Equals(object obj)
        {
            return obj is UriFilter uriFilter && Equals(uriFilter);
        }

        /// <summary>
        /// Returns a hash code for the current instance.
        /// </summary>
        /// <returns>The hash code for the current instance.</returns>
        public override int GetHashCode()
        {
            return (_uri, _exactMatch).GetHashCode();
        }

        /// <summary>
        /// Compares two uri filters.
        /// </summary>
        /// <param name="left">The first <see cref="UriFilter"/>.</param>
        /// <param name="right">The second <see cref="UriFilter"/>.</param>
        /// <returns>True if <paramref name="left"/> equals <paramref name="right"/>, false otherwise.</returns>
        public static bool operator ==(in UriFilter left, in UriFilter right)
        {
            return left.Equals(right);
        }

        /// <summary>
        /// Compares two uri filters for inequality.
        /// </summary>
        /// <param name="left">The first <see cref="UriFilter"/>.</param>
        /// <param name="right">The second <see cref="UriFilter"/>.</param>
        /// <returns>True if <paramref name="left"/> does not equal <paramref name="right"/>, false otherwise.</returns>
        public static bool operator !=(in UriFilter left, in UriFilter right)
        {
            return !left.Equals(right);
        }

        /// <summary>
        /// Returns a boolean value indicating whether the specified url matches the filter.
        /// </summary>
        /// <param name="uri">The uri to test.</param>
        /// <returns>True if <paramref name="uri"/> matches the filter, false otherwise.</returns>
        /// <exception cref="ArgumentNullException">Thrown if <paramref name="uri"/> is null.</exception>
        public bool IsMatch(string uri)
        {
            if (uri == null)
                throw new ArgumentNullException(nameof(uri));

            if (Equals(default))
            {
                return true;
            }

            var comparison = _uri.AsSpan();

            if (!uri.StartsWith("/"))
            {
                comparison = comparison.Slice(1);
            }

            if (_exactMatch)
            {
                return comparison.Equals(uri.AsSpan(), StringComparison.Ordinal);
            }
            else
            {
                return uri.AsSpan().StartsWith(comparison, StringComparison.Ordinal);
            }
        }
    }
}
