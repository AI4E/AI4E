using System;
using System.Collections.Generic;
using AI4E.Utils;

namespace BookStore.Alerts
{
    public interface IAlertMessages
    {
        AlertPlacement PlaceAlert(in AlertMessage alertMessage);
        void CancelAlert(in AlertPlacement alertPlacement);
    }
}
