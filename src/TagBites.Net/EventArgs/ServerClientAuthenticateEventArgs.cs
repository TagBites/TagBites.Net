namespace TagBites.Net;

/// <summary>
/// Provides data for the <see cref="E:TagBites.Net.Server.ClientAuthenticate"/> event.
/// </summary>
public class ServerClientAuthenticateEventArgs : EventArgs
{
    /// <summary>
    /// Gets client credentials.
    /// </summary>
    public ClientCredentials Credentials { get; }
    /// <summary>
    /// Gets or sets value indicating whether authentication procedure has been successful.
    /// </summary>
    public bool Authenticated { get; set; }
    /// <summary>
    /// Gets or sets client identity. It could be assign based on credentials.
    /// </summary>
    public object Identity { get; set; }

    internal ServerClientAuthenticateEventArgs(ClientCredentials credentials)
    {
        Credentials = credentials;
    }
}
