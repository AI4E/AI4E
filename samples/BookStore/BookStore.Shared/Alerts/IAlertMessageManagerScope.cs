using System;

namespace BookStore.Alerts
{
    public interface IAlertMessageManagerScope : IDisposable
    {
        IAlertMessageManager AlertMessageManager { get; }

        AlertPlacement PlaceAlert(in AlertMessage alertMessage);
        void CancelAlert(in AlertPlacement alertPlacement);
        void ClearAlerts();
    }
}
