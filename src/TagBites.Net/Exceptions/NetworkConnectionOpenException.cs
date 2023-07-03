namespace TagBites.Net;

/// <summary>
/// The exception that is thrown when error occurred while establishing connection.
/// </summary>
public class NetworkConnectionOpenException : Exception
{
    internal NetworkConnectionOpenException()
    { }
    internal NetworkConnectionOpenException(Exception innerException)
        : base(string.Empty, innerException)
    { }
}
