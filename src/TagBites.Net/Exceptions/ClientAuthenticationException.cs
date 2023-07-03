using System.Security.Authentication;

namespace TagBites.Net;

/// <summary>
/// The exception that is thrown for failed client authentication.
/// </summary>
public class ClientAuthenticationException : AuthenticationException
{
    /// <inheritdoc />
    public ClientAuthenticationException()
    { }
    /// <inheritdoc />
    public ClientAuthenticationException(Exception innerException)
        : base(string.Empty, innerException)
    { }
}
