using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public class NetworkSerializationException : System.Net.ProtocolViolationException
    {
        public Exception SerializationException { get; private set; }
        public string TypeName { get; }

        internal int InResponseToId { get; }

        internal NetworkSerializationException(string typeName, Exception serializationException, int inResponseToId)
            : base("Serialize/Deserialize exception.")
        {
            TypeName = typeName;
            SerializationException = serializationException;

            InResponseToId = inResponseToId;
        }
    }
}
