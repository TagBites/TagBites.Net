namespace TagBites.Net;

/// <summary>
/// Client credentials used for authentication.
/// </summary>
[Serializable]
public class ClientCredentials
{
    /// <summary>
    /// Gets or sets user name.
    /// </summary>
    public string UserName { get; set; }
    /// <summary>
    /// Gets or sets user password.
    /// </summary>
    public string Password { get; set; }
    /// <summary>
    /// Gets or sets authorization token.
    /// </summary>
    public string Token { get; set; }
}
