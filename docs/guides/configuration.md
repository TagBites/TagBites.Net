---
title: Configuration
description: Encoding and serializer used by a connection.
---

`NetworkConfig` defines basic network configuration like encoding or serializer. Both `Encoding` and `Serializer` are **read-only** properties - they can only be set via a constructor, not through an object initializer.

`Server` or `Client` instance can be created with custom configuration by passing `NetworkConfig` instance to their constructor.

To change default network configuration:

```csharp
NetworkConfig.Default = new NetworkConfig(...);
```

## Encoding

By default all text messages are encoded using `UTF8`.

## Serializer

By default [Json.NET](https://www.newtonsoft.com/json) is used for serialization. See [Serialization](serialization.md) for what goes through the serializer, the built-in implementation, and how to replace it.
