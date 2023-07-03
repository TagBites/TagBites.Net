using System;
using System.Diagnostics;
using System.IO;
using System.Text;

namespace TagBites.Net
{
    /// <summary>
    /// Network configuration.
    /// </summary>
    [DebuggerDisplay("Encoding: {Encoding.EncodingName}, Serializer: {Serializer}")]
    public class NetworkConfig
    {
        private static NetworkConfig s_default = new NetworkConfig();

        /// <summary>
        /// Gets or sets default network configuration.
        /// </summary>
        public static NetworkConfig Default
        {
            get => s_default;
            set => s_default = value ?? throw new ArgumentNullException(nameof(value));
        }

        /// <summary>
        /// Gets encoding used to encode/decode socket messages.
        /// Default: UTF8.
        /// </summary>
        public Encoding Encoding { get; }

        /// <summary>
        /// Gets serializer used to serialize/deserialize object send through socket.
        /// Default: proxy to <see cref="System.Runtime.Serialization.Formatters.Binary.BinaryFormatter"/>.
        /// </summary>
        public INetworkSerializer Serializer { get; }

        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class, with default configuration.
        /// </summary>
        public NetworkConfig()
        {
            Encoding = Encoding.UTF8;
            Serializer = new NewtonsoftJsonSerializer();
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class.
        /// </summary>
        /// <param name="encoding">Encoding used to encode/decode socket messages.</param>
        public NetworkConfig(Encoding encoding)
        {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            Serializer = new NewtonsoftJsonSerializer();
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class.
        /// </summary>
        /// <param name="encoding">Encoding used to encode/decode socket messages.</param>
        /// <param name="serializer">Serializer used to serialize/deserialize object send through socket.</param>
        public NetworkConfig(Encoding encoding, INetworkSerializer serializer)
        {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class.
        /// </summary>
        /// <param name="encoding">Encoding used to encode/decode socket messages.</param>
        /// <param name="serializeDelegate">Delegate used to serialize object send through socket.</param>
        /// <param name="deserializeDelegate">Delegate used to deserialize object send through socket.</param>
        public NetworkConfig(Encoding encoding, Action<Stream, object> serializeDelegate, Func<Stream, Type, object> deserializeDelegate)
        {
            Encoding = encoding ?? throw new ArgumentNullException(nameof(encoding));
            Serializer = new DelegateNetworkSerializer(
                serializeDelegate ?? throw new ArgumentNullException(nameof(serializeDelegate)),
                deserializeDelegate ?? throw new ArgumentNullException(nameof(deserializeDelegate)));
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class.
        /// </summary>
        /// <param name="serializer">Serializer used to serialize/deserialize object send through socket.</param>
        public NetworkConfig(INetworkSerializer serializer)
        {
            Encoding = Encoding.UTF8;
            Serializer = serializer ?? throw new ArgumentNullException(nameof(serializer));
        }
        /// <summary>
        /// Initializes a new instance of the <see cref="NetworkConfig"/> class.
        /// </summary>
        /// <param name="serializeDelegate">Delegate used to serialize object send through socket.</param>
        /// <param name="deserializeDelegate">Delegate used to deserialize object send through socket.</param>
        public NetworkConfig(Action<Stream, object> serializeDelegate, Func<Stream, Type, object> deserializeDelegate)
        {
            Encoding = Encoding.UTF8;
            Serializer = new DelegateNetworkSerializer(
                serializeDelegate ?? throw new ArgumentNullException(nameof(serializeDelegate)),
                deserializeDelegate ?? throw new ArgumentNullException(nameof(deserializeDelegate)));
        }
    }
}
