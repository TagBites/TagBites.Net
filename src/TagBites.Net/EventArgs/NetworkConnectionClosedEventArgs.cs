namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.Closed"/> event.
/// </summary>
public class NetworkConnectionClosedEventArgs : EventArgs
{
    /// <summary>
    /// Gets a exception. Returns <c>null</c> when connection has been closed normally.
    /// </summary>
    public Exception Exception { get; }

    internal NetworkConnectionClosedEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
