namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.NetworkConnection.Received"/> event.
/// </summary>
public class NetworkConnectionMessageEventArgs : EventArgs
{
    /// <summary>
    /// Gets a message.
    /// </summary>
    public object Message { get; }

    internal NetworkConnectionMessageEventArgs(object message)
    {
        Message = message;
    }
}
