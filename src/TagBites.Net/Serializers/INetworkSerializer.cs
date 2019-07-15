using System;
using System.IO;

namespace TagBites.Net
{
    /// <summary>
    /// Provides a mechanism for serialization/deserialization of network objects.
    /// </summary>
    public interface INetworkSerializer
    {
        /// <summary>
        /// Serializes object to stream.
        /// </summary>
        /// <param name="stream">Stream to write the serialized object.</param>
        /// <param name="value">Object to serialize.</param>
        void Serialize(Stream stream, object value);

        /// <summary>
        /// Deserializes object from stream.
        /// </summary>
        /// <param name="stream">Stream with object data.</param>
        /// <param name="type">Type of object to deserialize.</param>
        /// <returns>Deserialized object.</returns>
        object Deserialize(Stream stream, Type type);
    }
}
