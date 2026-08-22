---
title: Configuration
description: Encoding and serializer used by a connection.
---

[`NetworkConfig`](https://tagbites.com/api/tagbites.net.networkconfig/) defines basic network configuration like encoding or serializer.  
[`Server`](https://tagbites.com/api/tagbites.net.server/) or [`Client`](https://tagbites.com/api/tagbites.net.client/) instance can be created with custom configuration by passing `NetworkConfig` instance to their constructor.

To change default network configuration:

```csharp
NetworkConfig.Default = new NetworkConfig(...);
```

## Encoding

By default, all text messages are encoded using `UTF8`.

## Serializer

By default, [Newtonsoft.Json](https://www.newtonsoft.com/json) is used for serialization. See [Serialization](serialization.md) for more information.
