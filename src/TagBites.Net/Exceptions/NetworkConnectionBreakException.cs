using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public class NetworkConnectionBreakException : Exception
    {
        internal NetworkConnectionBreakException()
        { }
        internal NetworkConnectionBreakException(Exception innerException)
            : base(String.Empty, innerException)
        { }
    }
}
