---
title: Serialization
description: How values travel over the connection and how to replace the serializer.
---

## How values travel

Not every message goes through the serializer:

- `string`, primitive types and `DateTime` values are encoded directly with the configured `Encoding` - no serializer involved.
- `byte[]` is sent as raw bytes, also bypassing the serializer entirely - so sending binary data is cheap compared to serializing an equivalent wrapper object.
- Any other object is sent as its type name followed by the bytes produced by the configured serializer.

## Message size

There is no hard message-size limit enforced by the library beyond what fits in a 32-bit length prefix, but there is also no streaming or chunked-transfer API: the entire message is buffered in memory on both ends. For large files, chunk the data yourself (e.g. send fixed-size `byte[]` pieces) rather than sending one very large message.

## Default serializer

By default [Json.NET](https://www.newtonsoft.com/json) is used, with the following implementation:

```csharp
public class NewtonsoftJsonSerializer : INetworkSerializer
{
    private readonly JsonSerializer _serializer;

    public NewtonsoftJsonSerializer()
    {
        var settings = new JsonSerializerSettings
        {
            // Required for internal dynamic type handling
            TypeNameHandling = TypeNameHandling.Auto
        };
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
```

## Custom serializer

`NetworkConfig` accepts any implementation of `INetworkSerializer`:

```csharp
public interface INetworkSerializer
{
    void Serialize(Stream stream, object value);
    object Deserialize(Stream stream, Type type);
}
```

Example based on `System.Text.Json`:

```csharp
using System.Text.Json;

public class SystemTextJsonSerializer : INetworkSerializer
{
    private readonly JsonSerializerOptions _options;

    public SystemTextJsonSerializer()
    {
        // Mirrors the intent of TypeNameHandling.Auto in the built-in Json.NET serializer - only enable
        // polymorphic/dynamic type resolution if you fully trust the remote side (see the security note below).
        _options = new JsonSerializerOptions();
    }

    public void Serialize(Stream stream, object value)
    {
        JsonSerializer.Serialize(stream, value, value.GetType(), _options);
    }

    public object Deserialize(Stream stream, Type type)
    {
        return JsonSerializer.Deserialize(stream, type, _options);
    }
}
```

Pass it to a `NetworkConfig` constructor - per instance or as the application-wide default:

```csharp
NetworkConfig.Default = new NetworkConfig(new SystemTextJsonSerializer());

var client = new Client("127.0.0.1", 8200); // uses NetworkConfig.Default
// or, per-instance:
var client2 = new Client("127.0.0.1", 8200, new NetworkConfig(new SystemTextJsonSerializer()));
```

A serializer can also be defined as a pair of delegates, without implementing the interface:

```csharp
var config = new NetworkConfig(
    (stream, value) => MyFormat.Write(stream, value),
    (stream, type) => MyFormat.Read(stream, type));
```

Both sides of a connection must use the same serializer.

## Security

Both the built-in serializer and the example above involve deserializing types received over the network. If you enable polymorphic/dynamic type resolution (e.g. `TypeNameHandling.Auto` in Json.NET, or a custom `$type`-like mechanism), a malicious or compromised peer could potentially instruct your process to instantiate arbitrary types. Mitigate by:

- Only enabling this on connections where both ends are trusted and authenticated (see [Authentication](authentication.md)).
- Restricting deserialization to an allow-list of known types where possible.
- Keeping the transport itself encrypted (see [SSL/TLS](authentication.md#ssltls)).
