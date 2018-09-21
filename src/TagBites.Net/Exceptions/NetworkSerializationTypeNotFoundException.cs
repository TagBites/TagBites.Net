using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// The exception that is thrown when serialized type is not found.
    /// </summary>
    public class NetworkSerializationTypeNotFoundException : NetworkSerializationException
    {
        internal NetworkSerializationTypeNotFoundException(string typeName, int messageId, int inResponseToId)
            : base(typeName, null, messageId, inResponseToId)
        { }
    }
}
