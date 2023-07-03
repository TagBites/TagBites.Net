namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.ReceivedError"/> event.
/// </summary>
public class NetworkConnectionMessageErrorEventArgs : EventArgs
{
    /// <summary>
    /// Gets a exception.
    /// </summary>
    public Exception Exception { get; }

    internal NetworkConnectionMessageErrorEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
