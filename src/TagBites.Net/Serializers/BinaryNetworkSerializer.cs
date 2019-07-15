using System;
using System.IO;
using System.Runtime.Serialization.Formatters.Binary;

namespace TagBites.Net
{
    internal class BinaryNetworkSerializer : INetworkSerializer
    {
        private readonly BinaryFormatter _formatter = new BinaryFormatter();


        public void Serialize(Stream stream, object value)
        {
            _formatter.Serialize(stream, value);
        }
        public object Deserialize(Stream stream, Type type)
        {
            return _formatter.Deserialize(stream);
        }
    }
}
