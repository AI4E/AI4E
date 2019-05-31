using System;
using System.Collections.Generic;

namespace BookStore.Alerts
{
    public interface IAlertMessageManager : IAlertMessages, IDisposable
    {
        event EventHandler AlertsChanged;

        void Dismiss(Alert alert);
        Alert GetLatestMatchingAlert(string relativeUri);
        IReadOnlyList<Alert> GetMatchingAlerts(string relativeUri);

        IAlertMessageManagerScope CreateScope();
    }
}
