namespace TagBites.Net;

/// <summary>
/// The exception that is thrown when connection breaks.
/// </summary>
public class NetworkConnectionBreakException : Exception
{
    internal NetworkConnectionBreakException()
    { }
    internal NetworkConnectionBreakException(Exception innerException)
        : base(string.Empty, innerException)
    { }
}
