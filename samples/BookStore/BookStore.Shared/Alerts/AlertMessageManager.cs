using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Threading.Tasks;
using AI4E;
using AI4E.Utils;

namespace BookStore.Alerts
{
    public sealed class AlertMessageManager : IAlertMessageManager
    {
        private readonly IDateTimeProvider _dateTimeProvider;
        private readonly LinkedList<AlertMessage> _alertMessages = new LinkedList<AlertMessage>();
        private readonly object _mutex = new object();
        private bool _isDisposed = false;

        public AlertMessageManager(IDateTimeProvider dateTimeProvider)
        {
            if (dateTimeProvider == null)
                throw new ArgumentNullException(nameof(dateTimeProvider));

            _dateTimeProvider = dateTimeProvider;
        }

        internal void PlaceAlert(LinkedListNode<AlertMessage> node)
        {
            Debug.Assert(node != null);

            if (node.Value.Expiration == null)
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    _alertMessages.AddLast(node);
                }

                OnAlertsChanged();

                return;
            }

            var now = _dateTimeProvider.GetCurrentTime();
            var delay = (DateTime)node.Value.Expiration - now;

            // The message is expired already, do not add it.
            if (delay <= TimeSpan.Zero)
            {
                return;
            }

            // We have to add the message before scheduling the continuation
            // to prevent a race when delay is small and the continuation is
            // invoked before the message is added actually.
            lock (_mutex)
            {
                CheckDisposed();
                _alertMessages.AddLast(node);
            }

            OnAlertsChanged();

            Task.Delay(delay).ContinueWith(_ =>
            {
                lock (_mutex)
                {
                    if (!_isDisposed)
                    {
                        _alertMessages.Remove(node);
                    }
                }

                OnAlertsChanged();
            }).HandleExceptions();
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
            PlaceAlert(node);
            return new AlertPlacement(this, node);
        }

        public void CancelAlert(in AlertPlacement alertPlacement)
        {
            var node = alertPlacement.Node;

            lock (_mutex)
            {
                CheckDisposed();
                _alertMessages.Remove(node);
            }

            OnAlertsChanged();
        }

        public void Dismiss(Alert alert)
        {
            var node = alert.Node;

            lock (_mutex)
            {
                CheckDisposed();
                _alertMessages.Remove(node);
            }

            OnAlertsChanged();
        }

        public IReadOnlyList<Alert> GetMatchingAlerts(string relativeUri)
        {
            lock (_mutex)
            {
                CheckDisposed();

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
                CheckDisposed();

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

        private void CheckDisposed()
        {
            if (_isDisposed)
                throw new ObjectDisposedException(GetType().FullName);
        }

        public void Dispose()
        {
            lock (_mutex)
            {
                if (_isDisposed)
                    return;

                _isDisposed = true;

                _alertMessages.Clear();
                OnAlertsChanged();
            }
        }

        public IAlertMessageManagerScope CreateScope()
        {
            return new AlertMessageManagerScope(this);
        }

        private sealed class AlertMessageManagerScope : IAlertMessageManagerScope
        {
            private readonly HashSet<AlertPlacement> _alertPlacements = new HashSet<AlertPlacement>();
            private readonly AlertMessageManager _alertMessageManager;
            private readonly object _mutex = new object();
            private bool _isDisposed = false;

            public AlertMessageManagerScope(AlertMessageManager alertMessageManager)
            {
                _alertMessageManager = alertMessageManager;
            }

            public IAlertMessageManager AlertMessageManager => _alertMessageManager;

            public AlertPlacement PlaceAlert(in AlertMessage alertMessage)
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    var alertPlacement = _alertMessageManager.PlaceAlert(alertMessage);
                    _alertPlacements.Add(alertPlacement);
                    return alertPlacement;
                }
            }

            public void CancelAlert(in AlertPlacement alertPlacement)
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    _alertMessageManager.CancelAlert(alertPlacement);
                    _alertPlacements.Remove(alertPlacement);
                }
            }

            public void ClearAlerts()
            {
                lock (_mutex)
                {
                    CheckDisposed();
                    foreach (var alertPlacement in _alertPlacements)
                    {
                        _alertMessageManager.CancelAlert(alertPlacement);
                    }
                    _alertPlacements.Clear();
                }
            }

            private void CheckDisposed()
            {
                if (_isDisposed)
                    throw new ObjectDisposedException(GetType().FullName);
            }

            public void Dispose()
            {
                lock (_mutex)
                {
                    if (_isDisposed)
                        return;

                    _isDisposed = true;

                    foreach (var alertPlacement in _alertPlacements)
                    {
                        try
                        {
                            _alertMessageManager.CancelAlert(alertPlacement);
                        }
                        // TODO: Add a separate method that does not throw on disposal.
                        catch (ObjectDisposedException) { }
                    }

                    _alertPlacements.Clear();
                }
            }
        }
    }

    public static class AlertMessageManagerExtension
    {
        public static AlertPlacement PlaceAlert(
            this IAlertMessageManager alertMessageManager,
            AlertType alertType,
            string message,
            DateTime? expiration = null,
            bool allowDismiss = false,
            UriFilter uriFilter = default)
        {
            var alertMessage = new AlertMessage(alertType, message, expiration, allowDismiss, uriFilter);
            return alertMessageManager.PlaceAlert(alertMessage);
        }

        public static AlertPlacement PlaceAlert(
            this IAlertMessageManagerScope alertMessageManager,
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
