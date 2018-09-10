using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

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
        /// Error occured while receiving data.
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
        /// Exception occured while executing remote method.
        /// </summary>
        MethodInvokeException
    }

    /// <summary>
    /// The exception that is thrown when another exception occured while remote method invocation.
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

        internal NetworkControllerInvocationException(NetworkControllerInvocationExceptionType type, string remoteMessage)
            : this(type, remoteMessage, null)
        { }
        internal NetworkControllerInvocationException(NetworkControllerInvocationExceptionType type, string remoteMessage, Exception error)
            : base("Remote controller execution exception occured.", error)
        {
            Type = type;
            RemoteMessage = remoteMessage;
        }
    }
}
