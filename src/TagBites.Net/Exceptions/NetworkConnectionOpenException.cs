using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// The exception that is thrown when error occured while establishing connection.
    /// </summary>
    public class NetworkConnectionOpenException : Exception
    {
        internal NetworkConnectionOpenException()
        { }
        internal NetworkConnectionOpenException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
