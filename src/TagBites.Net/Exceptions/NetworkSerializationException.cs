using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// The exception that is thrown when another exception is thrown while serialize/deserialize object.
    /// </summary>
    public class NetworkSerializationException : System.Net.ProtocolViolationException
    {
        /// <summary>
        /// Gets a serialization exception.
        /// </summary>
        public Exception SerializationException { get; }
        /// <summary>
        /// Gets a type name.
        /// </summary>
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
