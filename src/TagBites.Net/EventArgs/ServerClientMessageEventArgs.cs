namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.Server.Received"/> event.
/// </summary>
public class ServerClientMessageEventArgs : ServerClientEventArgs
{
    /// <summary>
    /// Gets a message.
    /// </summary>
    public object Message { get; }

    internal ServerClientMessageEventArgs(ServerClient client, object message)
        : base(client)
    {
        Message = message;
    }
}
