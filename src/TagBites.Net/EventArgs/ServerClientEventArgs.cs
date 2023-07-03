namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="Server"/> events.
/// </summary>
public class ServerClientEventArgs : EventArgs
{
    /// <summary>
    /// Gets client for whom the event was called.
    /// </summary>
    public ServerClient Client { get; }

    internal ServerClientEventArgs(ServerClient client)
    {
        Client = client;
    }
}
