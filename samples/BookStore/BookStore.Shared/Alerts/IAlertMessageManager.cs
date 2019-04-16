using System;
using System.Collections.Generic;

namespace BookStore.Alerts
{
    public interface IAlertMessageManager : IDisposable
    {
        event EventHandler AlertsChanged;

        AlertPlacement PlaceAlert(in AlertMessage alertMessage);
        void CancelAlert(in AlertPlacement alertPlacement);
        void Dismiss(Alert alert);
        Alert GetLatestMatchingAlert(string relativeUri);
        IReadOnlyList<Alert> GetMatchingAlerts(string relativeUri);

        IAlertMessageManagerScope CreateScope();
    }
}
