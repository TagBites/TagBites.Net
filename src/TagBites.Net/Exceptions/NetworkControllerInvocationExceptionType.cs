namespace TagBites.Net;

/// <summary>
/// Type of remote method invocation exception
/// </summary>
public enum NetworkControllerInvocationExceptionType
{
    /// <summary>
    /// Operation has been cancelled.
    /// </summary>
    OperationCancelled,
    /// <summary>
    /// Error occurred while receiving data.
    /// </summary>
    DataReceivingError,
    /// <summary>
    /// Remote controller type not found.
    /// </summary>
    ControllerNotFound,
    /// <summary>
    /// Remote controller's method not found.
    /// </summary>
    MethodNotFound,
    /// <summary>
    /// Exception occurred while executing remote method.
    /// </summary>
    MethodInvokeException
}
