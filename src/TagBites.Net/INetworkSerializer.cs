using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace TagBites.Net
{
    public interface INetworkSerializer
    {
        object Deserialize(Stream stream, Type type);
        void Serialize(Stream stream, object value);
    }
}
