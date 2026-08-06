# TagBites.Net

Lightweight and simple TCP client-server .NET library with RMI support.

NuGet Package: https://www.nuget.org/packages/TagBites.Net/

## Chat example

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

In this example a `string` type is used for communication, but any serializable objects can be send/received. 

By default `Newtonsoft.Json` is used for serialization, but it can be replaced with a [custom implementation](docs/configuration.md). 

Full example in this repository: [samples/Chat](samples/Chat).

## Chat example using RMI (Remote Method Invocation)
    
### Client code
```csharp
var client = new Client("127.0.0.1", 10500);
client.Use<IChatClient, ChatClient>();
await client.ConnectAsync();

while (Console.ReadLine() is { } message)
    client.GetController<IChatServer>().Send(message);
```

The method `Use<TControllerInterface, TController>()` registers a controller that can be used by the server. The server site can use the `IChatClient` interface to execute methods implemented by `ChatClient` on the client's site. The client/server can registers many controllers. The controller instance will be created on first use.

`GetController<T>()` returns the proxy interface to the class register in the remote site. The calling method in this instance will invoke method in the remote site. The Controller can invoke methods with primitive or serializable parameter types and returns void/Task or any primitive or serializable type.

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
