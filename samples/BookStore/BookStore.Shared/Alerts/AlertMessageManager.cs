using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using AI4E;
using AI4E.Utils;

namespace BookStore.Alerts
{
    public sealed class AlertMessageManager
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly LinkedList<AlertMessage> _alertMessages = new LinkedList<AlertMessage>();
        private readonly object _mutex = new object();

        public AlertMessageManager(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        public AlertPlacement PlaceAlert(in AlertMessage alertMessage)
        {
            if (!alertMessage.AlertType.IsValid())
            {
                throw new ArgumentException($"The alert type must be one of the values defined in {typeof(AlertType)}.", nameof(alertMessage));
            }

            if (alertMessage.AlertType == AlertType.None)
            {
                return default;
            }

            var node = new LinkedListNode<AlertMessage>(alertMessage);

            if (alertMessage.Expiration == null)
            {

                lock (_mutex)
                {
                    _alertMessages.AddLast(node);
                }
                OnAlertsChanged();

                return new AlertPlacement(this, node);
            }

            var now = _dateTimeProvider.GetCurrentTime();
            var delay = (DateTime)alertMessage.Expiration - now;

            // The message is expired already, do not add it.
            if (delay <= TimeSpan.Zero)
            {
                return default;
            }

            // We have to add the message before scheduling the continuation
            // to prevent a race when delay is small and the continuation is
            // invoked before the message is added actually.
            lock (_mutex)
            {
                _alertMessages.AddLast(node);
            }
            OnAlertsChanged();

            Task.Delay(delay).ContinueWith(_ =>
            {
                lock (_mutex)
                {
                    _alertMessages.Remove(node);
                }
                OnAlertsChanged();
            }).HandleExceptions();

            return new AlertPlacement(this, node);
        }

        public void CancelAlert(in AlertPlacement alertPlacement)
        {
            var node = alertPlacement.Node;

            lock (_mutex)
            {
                _alertMessages.Remove(node);
            }
            OnAlertsChanged();
        }

        public void Dismiss(Alert alert)
        {
            var node = alert.Node;

            lock (_mutex)
            {
                _alertMessages.Remove(node);
            }
            OnAlertsChanged();
        }

        public IReadOnlyList<Alert> GetMatchingAlerts(string relativeUri)
        {
            lock (_mutex)
            {
                if (_alertMessages.Count == 0)
                {
                    return ImmutableList<Alert>.Empty;
                }

                if (_alertMessages.Count == 1)
                {
                    return ImmutableList.Create(new Alert(this, _alertMessages.First));
                }

                var builder = ImmutableList.CreateBuilder<Alert>();

                for (var current = _alertMessages.Last; current != null; current = current.Previous)
                {
                    if (current.Value.UriFilter.IsMatch(relativeUri))
                    {
                        builder.Add(new Alert(this, current));
                    }
                }

                return builder.ToImmutable();
            }
        }

        public Alert GetLatestMatchingAlert(string relativeUri)
        {
            lock (_mutex)
            {
                for (var current = _alertMessages.Last; current != null; current = current.Previous)
                {
                    if (current.Value.UriFilter.IsMatch(relativeUri))
                    {
                        return new Alert(this, current);
                    }
                }

                return default;
            }
        }

        public event EventHandler AlertsChanged;

        private void OnAlertsChanged()
        {
            AlertsChanged?.Invoke(this, EventArgs.Empty);
        }
    }

    public readonly struct AlertPlacement : IDisposable
    {
        private readonly AlertMessageManager _alertMessageManager;

        internal AlertPlacement(
            AlertMessageManager alertMessageManager,
            LinkedListNode<AlertMessage> node)
        {
            Debug.Assert(alertMessageManager != null);

            _alertMessageManager = alertMessageManager;
            Node = node;
        }

        internal LinkedListNode<AlertMessage> Node { get; }

        public void Dispose()
        {
            _alertMessageManager?.CancelAlert(this);
        }
    }

    public readonly struct Alert : IEquatable<Alert>
    {
        private readonly AlertMessageManager _alertMessageManager;

        internal Alert(
            AlertMessageManager alertMessageManager,
            LinkedListNode<AlertMessage> node)
        {
            Debug.Assert(alertMessageManager != null);
            Debug.Assert(node != null);
            _alertMessageManager = alertMessageManager;
            Node = node;
        }

        internal LinkedListNode<AlertMessage> Node { get; }

        public bool Equals(Alert other)
        {
            // TODO: Is it necessary to include the AlertMessageManager into comparison?
            // A node belongs to a single AlertMessageManager in its complete lifetime,
            // so it should be suffice to compare the nodes.

            return Node == other.Node;
        }

        public override bool Equals(object obj)
        {
            return obj is Alert alert && Equals(alert);
        }

        public override int GetHashCode()
        {
            // TODO: Is it necessary to include the AlertMessageManager into hash code generation?
            //       See Equals(Alert)

            return Node.GetHashCode();
        }

        public static bool operator ==(Alert left, Alert right)
        {
            return left.Equals(right);
        }

        public static bool operator !=(Alert left, Alert right)
        {
            return !left.Equals(right);
        }

        public bool IsExpired => Node == null || Node.List == null;

        /// <summary>
        /// Gets the type of alert.
        /// </summary>
        public AlertType AlertType => Node?.Value.AlertType ?? AlertType.None;

        /// <summary>
        /// Gets the alert message.
        /// </summary>
        public string Message => Node?.Value.Message;

        /// <summary>
        /// Gets a boolean value indicating whether the alert may be dismissed.
        /// </summary>
        public bool AllowDismiss => !IsExpired && Node.Value.AllowDismiss;

        // TODO: Do we need another property for the expiration?

        public void Dismiss()
        {
            _alertMessageManager?.Dismiss(this);
        }
    }

    public static class AlertMessageManagerExtension
    {
        public static AlertPlacement PlaceAlert(
            this AlertMessageManager alertMessageManager,
            AlertType alertType,
            string message,
            DateTime? expiration = null,
            bool allowDismiss = false,
            UriFilter uriFilter = default)
        {
            var alertMessage = new AlertMessage(alertType, message, expiration, allowDismiss, uriFilter);
            return alertMessageManager.PlaceAlert(alertMessage);
        }
    }
}
