# TagBites.Net

[![Nuget](https://img.shields.io/nuget/v/TagBites.Net.svg)](https://www.nuget.org/packages/TagBites.Net/)
![.NET Standard 2.0](https://img.shields.io/badge/.NET%20Standard-2.0-512BD4)
[![License](https://img.shields.io/github/license/TagBites/TagBites.Net)](https://github.com/TagBites/TagBites.Net/blob/master/LICENSE.md)
[![Downloads](https://img.shields.io/nuget/dt/TagBites.Net.svg)](https://www.nuget.org/packages/TagBites.Net/)

Lightweight and simple TCP client-server .NET library with RMI support.

## Install

```
dotnet add package TagBites.Net
```

Targets `netstandard2.0` and `net7.0`. The only dependency is `Newtonsoft.Json`, plus `System.Reflection.DispatchProxy` on `netstandard2.0`.

## Chat example

A console chat. Every client sends text lines to the server, and the server broadcasts each message to the other clients, together with connect and disconnect notifications.

### Client code
```csharp
var client = new Client("127.0.0.1", 8200);
client.Received += (s, e) => Console.WriteLine(e.Message.ToString());
await client.ConnectAsync();

while (Console.ReadLine() is { } message)
    await client.SendAsync(message);
```

### Server code
```csharp
var server = new Server("127.0.0.1", 8200);
server.ClientConnected += async (s, e) => await server.SendToAllAsync($"{e.Client} connected", e.Client);
server.ClientDisconnected += async (s, e) => await server.SendToAllAsync($"{e.Client} disconnected", e.Client);
server.Received += async (s, e) => await server.SendToAllAsync($"{e.Client}: {e.Message}", e.Client);
server.Listening = true; // starts a new thread

Console.ReadLine(); // for console application to prevent app from closing
```

In this example a `string` type is used for communication, but any serializable objects can be sent/received. 

By default `Newtonsoft.Json` is used for serialization, but it can be replaced with a [custom implementation](https://tagbites.com/net/guides/serialization). 

Full example in this repository: [samples/Chat](samples/Chat).

## Chat example using RMI (Remote Method Invocation)

The same chat, written as method calls instead of messages. Both sides share two interfaces: the client calls `IChatServer.Send` on the server, and the server calls `IChatClient.OnMessage` on every client. Serialization and dispatch happen behind the proxy returned by `GetController<T>()`.

### Client code
```csharp
var client = new Client("127.0.0.1", 10500);
client.Use<IChatClient, ChatClient>();
await client.ConnectAsync();

while (Console.ReadLine() is { } message)
    client.GetController<IChatServer>().Send(message);
```

The method `Use<TControllerInterface, TController>()` registers a controller that can be used by the server. The server side can use the `IChatClient` interface to execute methods implemented by `ChatClient` on the client's side. The client/server can register many controllers. The controller instance will be created on first use.

`GetController<T>()` returns a proxy implementing the interface registered on the remote side. Calling a method on it invokes that method on the remote side. A controller method takes primitive or serializable parameters and returns `void`, `Task`, or any primitive or serializable type.

### Server code
```csharp
var server = new Server("127.0.0.1", 10500);
server.Use<IChatServer, ChatServer>(client => new ChatServer { Client = client, Server = server });
server.ClientConnected += (s, e) =>
{
    foreach (var client in server.GetClients())
        if (client != e.Client)
            client.GetController<IChatClient>().OnMessage(null, $"Client {e.Client.Identity} connected");

    Console.WriteLine($"Client {e.Client.Identity} connected.");
};
server.ClientDisconnected += (s, e) =>
{
    foreach (var client in server.GetClients())
        client.GetController<IChatClient>().OnMessage(null, $"Client {e.Client.Identity} disconnected");

    Console.WriteLine($"Client {e.Client.Identity} disconnected.");
};
server.Listening = true;

Console.ReadLine();
```

### Classes
```csharp
public interface IChatClient
{
    void OnMessage(string userName, string message);
}
public class ChatClient : IChatClient 
{
    public void OnMessage(string userName, string message)
    {
        Console.WriteLine(userName == null ? message : $"{userName}: {message}");
    }
}

public interface IChatServer
{
    void Send(string message);
}
public class ChatServer : IChatServer 
{
    public Server Server { get; set; }
    public ServerClient Client { get; set; }

    public void Send(string message)
    {
        Console.WriteLine($"{Client.Identity}: {message}");

        foreach (var client in Server.GetClients())
            if (Client != client)
                client.GetController<IChatClient>().OnMessage(Client.Identity?.ToString(), message);
    }
}
```

Full example in this repository: [samples/ChatWithControllers](samples/ChatWithControllers).

## Limitations

There is no message size limit and no rate limiting, so both sides have to be trusted. Authentication is a callback, not a protocol, and credentials travel in plain text unless the connection uses a certificate.

## Links

- Guides: [Architecture](https://tagbites.com/net/guides/architecture/), [Authentication](https://tagbites.com/net/guides/authentication/), [Configuration](https://tagbites.com/net/guides/configuration/), [Serialization](https://tagbites.com/net/guides/serialization/), [RMI](https://tagbites.com/net/guides/rmi/), [Error handling](https://tagbites.com/net/guides/error-handling/)
- [Changelog](https://tagbites.com/net/changelog/)
