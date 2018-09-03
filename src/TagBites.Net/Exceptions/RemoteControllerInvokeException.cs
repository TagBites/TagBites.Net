using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public enum RemoteControllerInvokeExceptionType
    {
        None,
        OperationCancelled,
        DataReceivingError,
        ControllerNotFound,
        MethodNotFound,
        MethodInvokeException
    }

    public class RemoteControllerInvokeException : Exception
    {
        public RemoteControllerInvokeExceptionType Type { get; }
        public string RemoteMessage { get; }

        internal RemoteControllerInvokeException(RemoteControllerInvokeExceptionType type, string remoteMessage)
            : this(type, remoteMessage, null)
        { }
        internal RemoteControllerInvokeException(RemoteControllerInvokeExceptionType type, string remoteMessage, Exception error)
            : base("Remote controller execution exception occured.", error)
        {
            Type = type;
            RemoteMessage = remoteMessage;
        }
    }
}
