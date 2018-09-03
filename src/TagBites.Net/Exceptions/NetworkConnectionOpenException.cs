using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public class NetworkConnectionOpenException : Exception
    {
        internal NetworkConnectionOpenException()
        { }
        internal NetworkConnectionOpenException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
