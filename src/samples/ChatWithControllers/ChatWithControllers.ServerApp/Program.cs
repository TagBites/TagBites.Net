using TagBites.Net;

namespace ChatWithControllers.ServerApp;

internal class Program
{
    private static void Main()
    {
        var nextUserId = 0;

        var server = new Server("127.0.0.1", 10500);
        server.Use<IChatServer, ChatServer>(client => new ChatServer { Client = client, Server = server });
        server.ClientAuthenticate += (s, e) =>
        {
            e.Identity = Interlocked.Increment(ref nextUserId);
            e.Authenticated = true;
        };
        server.ClientConnected += (s, e) =>
        {
            foreach (var client in server.GetClients())
                client.GetController<IChatClient>().MessageReceive(null, $"Client {e.Client.Identity} connected.");

            Console.WriteLine($"Client {e.Client.Identity} connected.");
        };
        server.ClientDisconnected += (s, e) =>
        {
            foreach (var client in server.GetClients())
                client.GetController<IChatClient>().MessageReceive(null, $"Client {e.Client.Identity} disconnected.");

            Console.WriteLine($"Client {e.Client.Identity} disconnected.");
        };
        server.Listening = true;

        Console.ReadLine();
    }
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
                client.GetController<IChatClient>().MessageReceive(Client.Identity?.ToString(), message);
    }
}
