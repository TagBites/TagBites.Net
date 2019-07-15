using System;
using System.IO;
using Newtonsoft.Json;

namespace TagBites.Net.Tests
{
    internal class NewtonsoftJsonSerializer : INetworkSerializer
    {
        private readonly JsonSerializer _serializer;

        public NewtonsoftJsonSerializer()
        {
            var settings = new JsonSerializerSettings { TypeNameHandling = TypeNameHandling.Auto };
            _serializer = JsonSerializer.CreateDefault(settings);
        }


        public void Serialize(Stream stream, object value)
        {
            using var writer = new StreamWriter(stream);
            using var jsonWriter = new JsonTextWriter(writer);

            _serializer.Serialize(jsonWriter, value);
        }
        public object Deserialize(Stream stream, Type type)
        {
            using var reader = new StreamReader(stream);
            using var jsonReader = new JsonTextReader(reader);

            var value = _serializer.Deserialize(jsonReader, type);
            return value;
        }
    }
}