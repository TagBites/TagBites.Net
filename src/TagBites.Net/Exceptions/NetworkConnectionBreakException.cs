using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// The exception that is thrown when connection breaks.
    /// </summary>
    public class NetworkConnectionBreakException : Exception
    {
        internal NetworkConnectionBreakException()
        { }
        internal NetworkConnectionBreakException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
