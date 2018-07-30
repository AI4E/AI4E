namespace AI4E.SignalR
{
    /// <summary>
    /// Describes the reason a connect/reconnect request was rejected.
    /// </summary>
    public enum RejectReason : int
    {
        Unknown = 0,
        IdAlreadyAssigned = 1,
        BadClient = 2,
        SessionTerminated = 3,
        Canceled = 4
    }
}
