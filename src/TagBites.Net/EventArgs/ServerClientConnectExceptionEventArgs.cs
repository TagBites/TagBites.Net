namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.Server.ClientConnectingError"/> event.
/// </summary>
public class ServerClientConnectExceptionEventArgs : EventArgs
{
    /// <summary>
    /// Gets a exception.
    /// </summary>
    public Exception Exception { get; }

    internal ServerClientConnectExceptionEventArgs(Exception exception)
    {
        Exception = exception;
    }
}
