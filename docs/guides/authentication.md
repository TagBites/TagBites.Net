---
title: Authentication
description: Passing credentials from the client and accepting them on the server.
---

## Client side
```csharp
var credentials = new ClientCredentials()
{
    UserName = "user",
    Password = "password",
    // Token = "..."
};
var client = new Client("127.0.0.1", 8200, credentials);
```

A `ClientCredentials` object allows passing basic authentication info, but derived class instance is also supported.

## Server side
```csharp
var server = new Server("127.0.0.1", 8200);
server.ClientAuthenticate += (s, e) =>
{
    if (MyValidateMethod(e.Credentials))
    {
        e.Identity = e.Credentials.UserName;
        e.Authenticated = true;
    }
};

bool MyValidateMethod(ClientCredentials credentials) { ... }
```

To enable authentication, subscribe to `ClientAuthenticate` event. To authenticate the user, the `Authenticated` property must be set to `true`, otherwise `ClientAuthenticationException` exception will be thrown on the client's side.

## Identity fallback

If the handler sets `Authenticated = true` but does not explicitly set `Identity`, the server falls back to `Credentials.UserName`. If the client connects **without** passing a `ClientCredentials` instance at all, `Credentials` will be `null`, and the fallback throws a `NullReferenceException` during the connection procedure - the server reports it through `ClientConnectingError` and closes the connection. Either always set `Identity` explicitly, or make sure clients always pass a `ClientCredentials` object when authentication is enabled.

## SSL/TLS

Authentication runs over whatever transport the connection was opened with. To also encrypt the connection itself, pass a certificate to the server and connect the client with `ConnectSslAsync`:

```csharp
// Server: pass a certificate to enable SSL
var server = new Server("127.0.0.1", 8200, myX509Certificate);

// Client: connect over SSL
var client = new Client("127.0.0.1", 8200);
await client.ConnectSslAsync();          // or ConnectSslAsync("server-name") for cert name validation
```

Internally this wraps the socket in an `SslStream` and negotiates TLS 1.2 (and TLS 1.3 on .NET 7+). By default, the client rejects any certificate that has policy errors (e.g. self-signed, untrusted root). To accept such certificates (e.g. in development), subclass `Client` and override the `protected virtual bool OnValidateServerCertificate(...)` method.
