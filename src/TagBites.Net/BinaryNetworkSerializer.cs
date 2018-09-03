using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.Serialization.Formatters.Binary;
using System.Text;

namespace TagBites.Net
{
    internal class BinaryNetworkSerializer : INetworkSerializer
    {
        private readonly BinaryFormatter m_formatter = new BinaryFormatter();


        public object Deserialize(Stream stream, Type type)
        {
            return m_formatter.Deserialize(stream);
        }
        public void Serialize(Stream stream, object value)
        {
            m_formatter.Serialize(stream, value);
        }
    }
}
