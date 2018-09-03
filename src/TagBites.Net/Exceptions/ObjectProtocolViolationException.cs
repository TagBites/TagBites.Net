using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public class ObjectProtocolViolationException : System.Net.ProtocolViolationException
    {
        internal ObjectProtocolViolationException(Exception innerError)
            : base(innerError.Message)
        { }
    }
}
