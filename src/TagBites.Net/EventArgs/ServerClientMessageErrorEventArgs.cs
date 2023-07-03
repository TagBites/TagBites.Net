namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.Server.ReceivedError"/> event.
/// </summary>
public class ServerClientMessageErrorEventArgs : ServerClientEventArgs
{
    /// <summary>
    /// Gets a exception.
    /// </summary>
    public Exception Exception { get; }

    internal ServerClientMessageErrorEventArgs(ServerClient client, Exception exception)
        : base(client)
    {
        Exception = exception;
    }
}
