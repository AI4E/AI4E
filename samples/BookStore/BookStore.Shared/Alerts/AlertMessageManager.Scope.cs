using System;
using System.Collections.Generic;

namespace BookStore.Alerts
{
    public sealed partial class AlertMessageManager
    {
        public IAlertMessageManagerScope CreateScope()
        {
            return new Scope(this);
        }

        private sealed class Scope : IAlertMessageManagerScope
        {
            private readonly HashSet<AlertPlacement> _alertPlacements = new HashSet<AlertPlacement>();
            private readonly AlertMessageManager _alertMessageManager;
            private readonly object _mutex = new object();
            private bool _isDisposed = false;

            public Scope(AlertMessageManager alertMessageManager)
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
                    ClearAlerts(checkDisposal: true);
                }
            }

            private void ClearAlerts(bool checkDisposal)
            {
                foreach (var alertPlacement in _alertPlacements)
                {
                    _alertMessageManager.CancelAlert(alertPlacement, checkDisposal);
                }
                _alertPlacements.Clear();
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

                    ClearAlerts(checkDisposal: false);
                }
            }
        }
    }
}
