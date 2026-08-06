using TagBites.Net;

namespace Chat.ClientApp;

internal class Program
{
    private static async Task Main()
    {
        const string host = "127.0.0.1";
        const int port = 8200;

        var client = new Client(host, port);
        client.Received += (s, e) => Console.WriteLine(e.Message.ToString());
        await client.ConnectAsync();

        Console.WriteLine($"Connected to server {host}:{port}. Type a message and press Enter.");

        while (Console.ReadLine() is { } message)
            await client.SendAsync(message);
    }
}
