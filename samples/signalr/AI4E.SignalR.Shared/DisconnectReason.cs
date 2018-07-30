namespace AI4E.SignalR
{
    /// <summary>
    /// Describes the reason a participant closes the connection.
    /// </summary>
    public enum DisconnectReason : int
    {
        Unknown = 0,
        OrdinaryShutdown = 1,
        Failure = 2
    }
}
