namespace TagBites.Net;

/// <summary>
/// The exception that is thrown when object transfer protocol is violated.
/// </summary>
public class NetworkObjectProtocolViolationException : System.Net.ProtocolViolationException
{
    internal NetworkObjectProtocolViolationException(Exception innerError)
        : base(innerError.Message)
    { }
}
