using System;
using System.IO;

namespace TagBites.Net
{
    internal class DelegateNetworkSerializer : INetworkSerializer
    {
        private readonly Action<Stream, object> _serializeDelegate;
        private readonly Func<Stream, Type, object> _deserializeDelegate;

        public DelegateNetworkSerializer(Action<Stream, object> serializeDelegate, Func<Stream, Type, object> deserializeDelegate)
        {
            _serializeDelegate = serializeDelegate ?? throw new ArgumentNullException(nameof(serializeDelegate));
            _deserializeDelegate = deserializeDelegate ?? throw new ArgumentNullException(nameof(deserializeDelegate));
        }


        public void Serialize(Stream stream, object value) => _serializeDelegate(stream, value);
        public object Deserialize(Stream stream, Type type) => _deserializeDelegate(stream, type);
    }
}