using System;

namespace TagBites.Net
{
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

    /// <summary>
    /// The exception that is thrown when another exception occurred while remote method invocation.
    /// </summary>
    public class NetworkControllerInvocationException : Exception
    {
        /// <summary>
        /// Gets a type of exception.
        /// </summary>
        public NetworkControllerInvocationExceptionType Type { get; }
        /// <summary>
        /// Gets remote exception message.
        /// </summary>
        public string RemoteMessage { get; }
        /// <summary>
        /// Gets full remote exception message.
        /// </summary>
        public string RemoteException { get; internal set; }

        internal NetworkControllerInvocationException(NetworkControllerInvocationExceptionType type, string remoteMessage)
            : this(type, remoteMessage, null)
        { }
        internal NetworkControllerInvocationException(NetworkControllerInvocationExceptionType type, string remoteMessage, Exception error)
            : base("Remote controller execution exception occurred.", error)
        {
            Type = type;
            RemoteMessage = remoteMessage;
        }
    }
}
