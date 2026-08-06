using TagBites.Net;

namespace ChatWithControllers.ClientApp;

internal class Program
{
    private static async Task Main()
    {
        const string host = "127.0.0.1";
        const int port = 10500;

        var client = new Client(host, port);
        client.Use<IChatClient, ChatClient>();
        await client.ConnectAsync();

        Console.WriteLine($"Connected to server {host}:{port}. Type a message and press Enter.");

        while (Console.ReadLine() is { } message)
            client.GetController<IChatServer>().Send(message);
    }
}

public class ChatClient : IChatClient
{
    public void OnMessage(string userName, string message)
    {
        Console.WriteLine(userName == null ? message : $"{userName}: {message}");
    }
}
