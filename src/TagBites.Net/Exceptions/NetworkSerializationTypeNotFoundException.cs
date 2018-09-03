using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public class NetworkSerializationTypeNotFoundException : NetworkSerializationException
    {
        internal NetworkSerializationTypeNotFoundException(string typeName, int inResponseToId)
            : base(typeName, null, inResponseToId)
        { }
    }
}
