using System;

namespace BookStore.Alerts
{
    public interface IAlertMessageManagerScope : IAlertMessages, IDisposable
    {
        IAlertMessageManager AlertMessageManager { get; }
        void ClearAlerts();
    }
}
